using FoiaProcessor.Agents.Workflow;
using FoiaProcessor.Api.Contracts;
using FoiaProcessor.Data;
using FoiaProcessor.Data.Entities;
using FoiaProcessor.Data.Audit;
using FoiaProcessor.McpTools.Contracts;
using FoiaProcessor.McpTools.Servers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoiaProcessor.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/documents")]
public class DocumentsController : ControllerBase
{
    private readonly FoiaDbContext _db;
    private readonly ReviewServer _review;
    private readonly WorkflowQueue _queue;
    private readonly IAuditWriter _audit;

    public DocumentsController(FoiaDbContext db, ReviewServer reviewServer, WorkflowQueue queue, IAuditWriter audit)
    {
        _db = db;
        _review = reviewServer;
        _queue = queue;
        _audit = audit;
    }

    [HttpGet("{id:guid}/review")]
    public async Task<IActionResult> GetReview(Guid id, CancellationToken ct)
    {
        var doc = await _db.Documents
            .AsNoTracking()
            .Include(d => d.Redactions)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
        if (doc is null) return NotFound(new { title = "Not Found", status = 404 });

        var dto = new DocumentReviewDto(
            doc.Id,
            doc.FoiaRequestId,
            doc.FileName,
            doc.OriginalContent,
            doc.RedactedContent,
            doc.Redactions
                .OrderBy(r => r.StartOffset)
                .Select(r => new DocumentRedactionDto(
                    r.Id, r.PiiType.ToString(), r.OriginalText, r.ReplacementText,
                    r.StartOffset, r.EndOffset, r.PageNumber, r.Confidence,
                    r.DetectionSource.ToString(), r.ReviewerApproved, r.ReviewerComments))
                .ToList(),
            doc.ReviewStatus.ToString());
        return Ok(dto);
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveDocumentRequestDto dto, CancellationToken ct)
    {
        var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (doc is null) return NotFound(new { title = "Not Found", status = 404 });

        await _review.RecordDocumentApprovalAsync(new RecordDocumentApprovalInput(id, dto?.Comments), ct);

        return Ok(new ApproveDocumentResponseDto(id, nameof(ReviewStatus.Approved), DateTime.UtcNow));
    }

    [HttpPost("{id:guid}/redactions")]
    public async Task<IActionResult> AddRedaction(Guid id, [FromBody] CreateRedactionRequestDto dto, CancellationToken ct)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.OriginalText) ||
            string.IsNullOrWhiteSpace(dto.ReplacementText) ||
            !Enum.TryParse<PiiType>(dto.PiiType, true, out var piiType))
        {
            return BadRequest(new { title = "A valid redaction type and text are required.", status = 400 });
        }

        var doc = await _db.Documents.Include(d => d.Redactions).FirstOrDefaultAsync(d => d.Id == id, ct);
        if (doc is null) return NotFound(new { title = "Not Found", status = 404 });
        var redaction = new Redaction
        {
            DocumentId = id,
            PiiType = piiType,
            OriginalText = dto.OriginalText,
            ReplacementText = dto.ReplacementText,
            StartOffset = dto.StartOffset,
            EndOffset = dto.EndOffset,
            PageNumber = dto.PageNumber,
            DetectionSource = DetectionSource.Ai,
            ReviewerApproved = true,
        };
        doc.Redactions.Add(redaction);
        RebuildRedactedContent(doc);
        await _db.SaveChangesAsync(ct);
        await _audit.RecordAsync(doc.FoiaRequestId, AuditEventType.RedactionAdded,
            $"Reviewer added a {piiType} redaction to '{doc.FileName}'.", id, ct);
        return Ok(new DocumentRedactionDto(redaction.Id, redaction.PiiType.ToString(), redaction.OriginalText,
            redaction.ReplacementText, redaction.StartOffset, redaction.EndOffset, redaction.PageNumber,
            redaction.Confidence, redaction.DetectionSource.ToString(), redaction.ReviewerApproved,
            redaction.ReviewerComments));
    }

    [HttpPatch("{id:guid}/redactions/{redactionId:guid}")]
    public async Task<IActionResult> UpdateRedaction(Guid id, Guid redactionId,
        [FromBody] UpdateRedactionRequestDto dto, CancellationToken ct)
    {
        var redaction = await _db.Redactions.Include(r => r.Document)
            .FirstOrDefaultAsync(r => r.Id == redactionId && r.DocumentId == id, ct);
        if (redaction is null) return NotFound(new { title = "Not Found", status = 404 });
        if (dto is null || string.IsNullOrWhiteSpace(dto.ReplacementText))
            return BadRequest(new { title = "Replacement text is required.", status = 400 });
        redaction.ReplacementText = dto.ReplacementText;
        redaction.ReviewerComments = dto.ReviewerComments;
        redaction.ReviewerApproved = true;
        RebuildRedactedContent(redaction.Document!);
        await _db.SaveChangesAsync(ct);
        await _audit.RecordAsync(redaction.Document!.FoiaRequestId, AuditEventType.RedactionUpdated,
            $"Reviewer updated a redaction in '{redaction.Document.FileName}'.", id, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/redactions/{redactionId:guid}")]
    public async Task<IActionResult> RemoveRedaction(Guid id, Guid redactionId, CancellationToken ct)
    {
        var redaction = await _db.Redactions.Include(r => r.Document)
            .FirstOrDefaultAsync(r => r.Id == redactionId && r.DocumentId == id, ct);
        if (redaction is null) return NotFound(new { title = "Not Found", status = 404 });
        var document = redaction.Document!;
        _db.Redactions.Remove(redaction);
        RebuildRedactedContent(document, redaction);
        await _db.SaveChangesAsync(ct);
        await _audit.RecordAsync(document.FoiaRequestId, AuditEventType.RedactionRemoved,
            $"Reviewer removed a redaction from '{document.FileName}'.", id, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/release-selection")]
    public async Task<IActionResult> SetReleaseSelection(Guid id, [FromBody] ReleaseSelectionRequestDto dto, CancellationToken ct)
    {
        var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (doc is null) return NotFound(new { title = "Not Found", status = 404 });
        doc.IncludeInRelease = dto.IncludeInRelease;
        await _db.SaveChangesAsync(ct);
        await _audit.RecordAsync(doc.FoiaRequestId, AuditEventType.DocumentReleaseSelectionChanged,
            $"Reviewer {(doc.IncludeInRelease ? "included" : "excluded")} '{doc.FileName}' from the release package.", id, ct);
        return Ok(new { id, doc.IncludeInRelease });
    }

    private static void RebuildRedactedContent(Document document, Redaction? removed = null)
    {
        var content = document.OriginalContent;
        foreach (var r in document.Redactions
                     .Where(r => r != removed && r.StartOffset.HasValue && r.EndOffset.HasValue)
                     .OrderByDescending(r => r.StartOffset))
        {
            var start = Math.Clamp(r.StartOffset!.Value, 0, content.Length);
            var end = Math.Clamp(r.EndOffset!.Value, start, content.Length);
            content = content[..start] + r.ReplacementText + content[end..];
        }
        document.RedactedContent = content;
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectDocumentRequestDto dto, CancellationToken ct)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.Comments))
        {
            return BadRequest(new
            {
                title = "Comments are required when rejecting a document.",
                status = 400,
                errors = new Dictionary<string, string[]> { ["comments"] = new[] { "Comments are required." } },
            });
        }

        var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (doc is null) return NotFound(new { title = "Not Found", status = 404 });

        await _review.RecordDocumentRejectionAsync(new RecordDocumentRejectionInput(id, dto.Comments), ct);

        return Ok(new RejectDocumentResponseDto(id, nameof(ReviewStatus.ManualHandling), DateTime.UtcNow));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var doc = await _db.Documents
            .Include(d => d.Redactions)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
        if (doc is null) return NotFound(new { title = "Not Found", status = 404 });

        _db.Documents.Remove(doc);
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }
}
