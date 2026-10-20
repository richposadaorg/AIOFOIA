using FoiaProcessor.Agents.Workflow;
using FoiaProcessor.Api.Contracts;
using FoiaProcessor.Api.Validation;
using FoiaProcessor.Data;
using FoiaProcessor.Data.Entities;
using FoiaProcessor.McpTools.Contracts;
using FoiaProcessor.McpTools.Servers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoiaProcessor.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/foiarequests")]
public class FoiaRequestsController : ControllerBase
{
    private readonly CaseServer _case;
    private readonly ReviewServer _review;
    private readonly BlobStorageServer _blob;
    private readonly FoiaDbContext _db;
    private readonly WorkflowQueue _queue;
    private readonly ILogger<FoiaRequestsController> _logger;

    public FoiaRequestsController(
        CaseServer caseServer,
        ReviewServer reviewServer,
        BlobStorageServer blobServer,
        FoiaDbContext db,
        WorkflowQueue queue,
        ILogger<FoiaRequestsController> logger)
    {
        _case = caseServer;
        _review = reviewServer;
        _blob = blobServer;
        _db = db;
        _queue = queue;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] SubmitFoiaRequestDto dto, CancellationToken ct)
    {
        var errors = FoiaRequestValidator.Validate(dto);
        if (errors.Count > 0)
        {
            return BadRequest(new
            {
                title = "One or more validation errors occurred.",
                status = 400,
                errors,
            });
        }

        var created = await _case.CaseCreateAsync(new CaseCreateInput(
            dto.Subject,
            dto.Description,
            dto.RequestedStartDate,
            dto.RequestedEndDate,
            dto.RequestorFullName,
            dto.RequestorOrganization,
            dto.RequestorEmail,
            dto.RequestorPhone,
            dto.RequestorMailingAddress), ct);

        await _queue.EnqueueAsync(created.CaseId, ct);

        var submittedAt = await _db.FoiaRequests
            .Where(r => r.Id == created.CaseId)
            .Select(r => r.SubmittedAt)
            .FirstAsync(ct);

        return Ok(new SubmitFoiaRequestResponseDto(created.CaseId, created.Status, submittedAt));
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int take, CancellationToken ct)
    {
        var clamped = Math.Clamp(take <= 0 ? 10 : take, 1, 100);
        var summaries = await _db.FoiaRequests
            .AsNoTracking()
            .OrderByDescending(r => r.SubmittedAt)
            .Take(clamped)
            .Select(r => new FoiaRequestSummaryDto(
                r.Id,
                r.Subject,
                r.RequestorFullName,
                r.Status.ToString(),
                r.SubmittedAt))
            .ToListAsync(ct);
        return Ok(summaries);
    }

    [HttpGet("paged")]
    public async Task<IActionResult> ListPaged(
        [FromQuery] int skip,
        [FromQuery] int take,
        CancellationToken ct)
    {
        var clampedTake = Math.Clamp(take <= 0 ? 25 : take, 1, 200);
        var clampedSkip = Math.Max(0, skip);

        var query = _db.FoiaRequests.AsNoTracking();
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(r => r.SubmittedAt)
            .Skip(clampedSkip)
            .Take(clampedTake)
            .Select(r => new FoiaRequestSummaryDto(
                r.Id,
                r.Subject,
                r.RequestorFullName,
                r.Status.ToString(),
                r.SubmittedAt))
            .ToListAsync(ct);

        return Ok(new PagedFoiaRequestsDto(total, clampedSkip, clampedTake, items));
    }

    [HttpGet("pending-review")]
    public async Task<IActionResult> ListPendingReview(CancellationToken ct)
    {
        var summaries = await _db.FoiaRequests
            .AsNoTracking()
            .Where(r => r.Status == RequestStatus.PendingHumanReview)
            .OrderByDescending(r => r.SubmittedAt)
            .Select(r => new FoiaRequestSummaryDto(
                r.Id,
                r.Subject,
                r.RequestorFullName,
                r.Status.ToString(),
                r.SubmittedAt))
            .ToListAsync(ct);
        return Ok(summaries);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var request = await _db.FoiaRequests
            .AsNoTracking()
            .Include(r => r.Documents)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (request is null)
        {
            return NotFound(new { title = "Not Found", status = 404, detail = $"FoiaRequest {id} not found." });
        }

        var auditEvents = await _db.AuditEvents
            .AsNoTracking()
            .Where(e => e.FoiaRequestId == id)
            .OrderBy(e => e.Timestamp)
            .Select(e => new
            {
                e.Timestamp,
                e.EventType,
                e.Message,
                e.RelatedDocumentId,
                FileName = e.RelatedDocumentId == null
                    ? null
                    : _db.Documents.Where(d => d.Id == e.RelatedDocumentId).Select(d => d.FileName).FirstOrDefault(),
            })
            .ToListAsync(ct);

        var counts = new CountsDto(
            DocumentsFound: request.Documents.Count,
            DocumentsPendingReview: request.Documents.Count(d => d.ReviewStatus == ReviewStatus.Pending || d.ReviewStatus == ReviewStatus.NotStarted),
            DocumentsApproved: request.Documents.Count(d => d.ReviewStatus == ReviewStatus.Approved),
            DocumentsRejected: request.Documents.Count(d => d.ReviewStatus == ReviewStatus.ManualHandling),
            DocumentsIncludedInRelease: request.Documents.Count(d => d.IncludeInRelease),
            DocumentsIncludedNotReady: request.Documents.Count(d => d.IncludeInRelease
                && (d.ReviewStatus != ReviewStatus.Approved
                    || d.RedactionStatus != RedactionStatus.Completed
                    || d.RedactedContent is null)));

        var latestPackage = await _db.ReleasePackages
            .AsNoTracking()
            .Where(p => p.FoiaRequestId == id)
            .OrderByDescending(p => p.Version)
            .FirstOrDefaultAsync(ct);
        var release = latestPackage is { } pkg
            ? new ReleaseDto("Ready", pkg.SasUrl, pkg.SasExpiresAt, pkg.Version)
            : new ReleaseDto("NotReady", null, null, null);

        var dto = new FoiaRequestStatusDto(
            request.Id,
            request.Subject,
            request.Description,
            request.RequestorFullName,
            request.RequestorEmail,
            request.RequestorOrganization,
            request.RequestorPhone,
            request.RequestorMailingAddress,
            request.RequestedStartDate,
            request.RequestedEndDate,
            request.Status.ToString(),
            request.SubmittedAt,
            counts,
            release,
            auditEvents.Select(e => new AuditEventDto(
                e.Timestamp, e.EventType.ToString(), e.Message, e.RelatedDocumentId, e.FileName)).ToList());

        return Ok(dto);
    }

    [HttpGet("{id:guid}/documents")]
    public async Task<IActionResult> ListDocuments(Guid id, CancellationToken ct)
    {
        var exists = await _db.FoiaRequests.AnyAsync(r => r.Id == id, ct);
        if (!exists) return NotFound(new { title = "Not Found", status = 404 });

        var docs = await _db.Documents
            .AsNoTracking()
            .Where(d => d.FoiaRequestId == id)
            .Select(d => new DocumentSummaryDto(
                d.Id,
                d.FileName,
                d.FileType,
                d.RedactionStatus.ToString(),
                d.ReviewStatus.ToString(),
                d.Redactions.Count,
                d.IncludeInRelease))
            .ToListAsync(ct);

        return Ok(new DocumentsListDto(id, docs));
    }

    [HttpPost("{id:guid}/approve-release")]
    public async Task<IActionResult> ApproveRelease(Guid id, CancellationToken ct)
    {
        var request = await _db.FoiaRequests
            .Include(r => r.Documents)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
        if (request is null) return NotFound(new { title = "Not Found", status = 404 });

        var selectedDocuments = request.Documents.Where(d => d.IncludeInRelease).ToList();
        if (selectedDocuments.Count == 0)
        {
            return Conflict(new
            {
                title = "No documents selected for release.",
                status = 409,
                detail = "Select at least one document before approving the release.",
            });
        }
        var notReady = selectedDocuments.Where(d => d.ReviewStatus != ReviewStatus.Approved
            || d.RedactionStatus != RedactionStatus.Completed
            || d.RedactedContent is null).ToList();
        if (notReady.Count > 0)
        {
            return Conflict(new
            {
                title = "Selected documents are not ready.",
                status = 409,
                detail = "Every included document must be redacted and approved. Exclude documents that are not ready for release.",
                documentIds = notReady.Select(d => d.Id),
            });
        }

        await _review.RecordReleaseApprovalAsync(new RecordReleaseApprovalInput(id, null), ct);
        await _case.CaseUpdateStatusAsync(
            new CaseUpdateStatusInput(id, nameof(RequestStatus.ApprovedForRelease), "Reviewer approved release."),
            ct);
        await _queue.EnqueueAsync(id, ct);

        return Ok(new ApproveReleaseResponseDto(id, nameof(RequestStatus.ApprovedForRelease), DateTime.UtcNow));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var request = await _db.FoiaRequests
            .FirstOrDefaultAsync(r => r.Id == id, ct);
        if (request is null)
        {
            return NotFound(new { title = "Not Found", status = 404, detail = $"FoiaRequest {id} not found." });
        }

        // Best-effort: delete the release package blob from storage before
        // removing the parent row. Storage failures are logged but do not
        // block the database delete (the row would otherwise be orphaned
        // pointing at storage we cannot reach).
        var packages = await _db.ReleasePackages
            .Where(p => p.FoiaRequestId == id)
            .ToListAsync(ct);
        foreach (var pkg in packages)
        {
            if (string.IsNullOrWhiteSpace(pkg.BlobContainerName) || string.IsNullOrWhiteSpace(pkg.ZipBlobName))
                continue;
            try
            {
                await _blob.DeleteBlobAsync(
                    new DeleteBlobInput(pkg.BlobContainerName, pkg.ZipBlobName), ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to delete release blob {Container}/{Blob} for request {RequestId}; continuing with DB delete.",
                    pkg.BlobContainerName, pkg.ZipBlobName, id);
            }
        }

        // FK cascade (configured in FoiaDbContext) removes Documents, Redactions,
        // ReviewTasks, ReleasePackage, and AuditEvents when the parent is deleted.
        _db.FoiaRequests.Remove(request);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/release")]
    public async Task<IActionResult> GetRelease(Guid id, CancellationToken ct)
    {
        var pkg = await _db.ReleasePackages
            .AsNoTracking()
            .Include(p => p.Documents)
            .Where(p => p.FoiaRequestId == id)
            .OrderByDescending(p => p.Version)
            .FirstOrDefaultAsync(ct);
        if (pkg is null)
        {
            return Ok(new ReleasePackageDto(id, "NotReady", null, null, null, null, null, null, []));
        }
        return Ok(new ReleasePackageDto(
            id,
            "Ready",
            pkg.ZipBlobName,
            pkg.BlobContainerName,
            pkg.SasUrl,
            pkg.SasExpiresAt,
            pkg.CreatedAt,
            pkg.Version,
            pkg.Documents.OrderByDescending(d => d.Included).ThenBy(d => d.FileName)
                .Select(d => new ReleasePackageDocumentDto(
                    d.DocumentId, d.FileName, d.PackageFileName, d.Included, d.ContentSha256))
                .ToList()));
    }

    [HttpGet("{id:guid}/release-packages")]
    public async Task<IActionResult> ListReleasePackages(Guid id, CancellationToken ct)
    {
        var exists = await _db.FoiaRequests.AnyAsync(r => r.Id == id, ct);
        if (!exists) return NotFound(new { title = "Not Found", status = 404 });

        var packages = await _db.ReleasePackages
            .AsNoTracking()
            .Include(p => p.Documents)
            .Where(p => p.FoiaRequestId == id)
            .OrderByDescending(p => p.Version)
            .Select(p => new ReleasePackageDto(
                id,
                "Ready",
                p.ZipBlobName,
                p.BlobContainerName,
                p.SasUrl,
                p.SasExpiresAt,
                p.CreatedAt,
                p.Version,
                p.Documents.OrderByDescending(d => d.Included).ThenBy(d => d.FileName)
                    .Select(d => new ReleasePackageDocumentDto(
                        d.DocumentId, d.FileName, d.PackageFileName, d.Included, d.ContentSha256))
                    .ToList()))
            .ToListAsync(ct);
        return Ok(packages);
    }
}
