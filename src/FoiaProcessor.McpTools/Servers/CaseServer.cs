using FoiaProcessor.Data;
using FoiaProcessor.Data.Audit;
using FoiaProcessor.Data.Entities;
using FoiaProcessor.McpTools.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FoiaProcessor.McpTools.Servers;

/// <summary>
/// Case server: persists FOIA request lifecycle state via the database.
/// All side-effecting operations also write an AuditEvent.
/// </summary>
public class CaseServer
{
    private readonly FoiaDbContext _db;
    private readonly IAuditWriter _audit;

    public CaseServer(FoiaDbContext db, IAuditWriter audit)
    {
        _db = db;
        _audit = audit;
    }

    public virtual async Task<CaseCreateOutput> CaseCreateAsync(CaseCreateInput input, CancellationToken ct = default)
    {
        var request = new FoiaRequest
        {
            Subject = input.Subject,
            Description = input.Description,
            RequestedStartDate = input.RequestedStartDate,
            RequestedEndDate = input.RequestedEndDate,
            RequestorFullName = input.RequestorFullName,
            RequestorOrganization = input.RequestorOrganization,
            RequestorEmail = input.RequestorEmail,
            RequestorPhone = input.RequestorPhone,
            RequestorMailingAddress = input.RequestorMailingAddress,
            Status = RequestStatus.Submitted,
            SubmittedAt = DateTime.UtcNow,
        };

        _db.FoiaRequests.Add(request);
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAsync(request.Id, AuditEventType.RequestSubmitted, "Request created.", ct: ct);
        return new CaseCreateOutput(request.Id, request.Status.ToString());
    }

    public virtual async Task<CaseGetOutput> CaseGetAsync(CaseGetInput input, CancellationToken ct = default)
    {
        var request = await _db.FoiaRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == input.CaseId, ct)
            ?? throw new KeyNotFoundException($"FoiaRequest {input.CaseId} not found.");
        return new CaseGetOutput(request.Id, request.Status.ToString(), request.Subject, request.RequestorFullName, request.SubmittedAt);
    }

    public virtual async Task<CaseUpdateStatusOutput> CaseUpdateStatusAsync(CaseUpdateStatusInput input, CancellationToken ct = default)
    {
        var request = await _db.FoiaRequests.FirstOrDefaultAsync(r => r.Id == input.CaseId, ct)
            ?? throw new KeyNotFoundException($"FoiaRequest {input.CaseId} not found.");

        if (!Enum.TryParse<RequestStatus>(input.NewStatus, ignoreCase: true, out var newStatus))
            throw new ArgumentException($"Unknown RequestStatus '{input.NewStatus}'.", nameof(input));

        request.Status = newStatus;
        await _db.SaveChangesAsync(ct);

        var auditType = MapStatusToAuditEventType(newStatus);
        var auditId = await _audit.RecordAsync(request.Id, auditType, input.Message, ct: ct);
        return new CaseUpdateStatusOutput(request.Id, request.Status.ToString(), auditId);
    }

    public virtual async Task<CaseAddNoteOutput> CaseAddNoteAsync(CaseAddNoteInput input, CancellationToken ct = default)
    {
        var auditId = await _audit.RecordAsync(input.CaseId, AuditEventType.RequestValidated, input.Message, input.RelatedDocumentId, ct);
        return new CaseAddNoteOutput(auditId);
    }

    public virtual async Task<CaseSaveDocumentMetadataOutput> CaseSaveDocumentMetadataAsync(CaseSaveDocumentMetadataInput input, CancellationToken ct = default)
    {
        var docs = input.Documents.Select(d => new Document
        {
            FoiaRequestId = input.CaseId,
            SourceDocumentId = d.SourceDocumentId,
            FileName = d.FileName,
            FileType = d.FileType,
            SourceUri = d.SourceUri,
            OriginalContent = d.OriginalContent,
            RetrievedAt = DateTime.UtcNow,
        }).ToList();

        _db.Documents.AddRange(docs);
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAsync(input.CaseId, AuditEventType.DocumentsFound, $"{docs.Count} document(s) saved.", ct: ct);
        return new CaseSaveDocumentMetadataOutput(docs.Select(d => d.Id).ToList());
    }

    public virtual async Task<CaseSaveRedactionResultsOutput> CaseSaveRedactionResultsAsync(CaseSaveRedactionResultsInput input, CancellationToken ct = default)
    {
        // Make this operation idempotent so the workflow can safely resume from
        // a previous partial run (Status == Redacting) without producing
        // DbUpdateConcurrencyExceptions caused by stale tracker state or
        // previously-persisted Redaction rows.
        var doc = await _db.Documents
            .Include(d => d.Redactions)
            .FirstOrDefaultAsync(d => d.Id == input.DocumentId, ct)
            ?? throw new KeyNotFoundException($"Document {input.DocumentId} not found.");

        // Remove any existing redactions for this document before re-creating
        // them. Using the DbSet (rather than the navigation collection) ensures
        // EF marks each row as Deleted explicitly.
        if (doc.Redactions.Count > 0)
        {
            _db.Redactions.RemoveRange(doc.Redactions);
        }

        doc.RedactedContent = input.RedactedContent;
        doc.RedactionStatus = RedactionStatus.Completed;
        doc.ReviewStatus = ReviewStatus.Pending;

        var newRedactions = input.Redactions.Select(f => new Redaction
        {
            DocumentId = doc.Id,
            PiiType = Enum.Parse<PiiType>(f.PiiType, ignoreCase: true),
            OriginalText = f.OriginalText,
            ReplacementText = f.ReplacementText,
            StartOffset = f.StartOffset,
            EndOffset = f.EndOffset,
            PageNumber = f.PageNumber,
            Confidence = f.Confidence,
            DetectionSource = Enum.Parse<DetectionSource>(f.DetectionSource, ignoreCase: true),
        }).ToList();

        // Add via the DbSet to guarantee an Added state (avoids relying on
        // navigation-collection fix-up across reused scoped DbContexts).
        if (newRedactions.Count > 0)
        {
            await _db.Redactions.AddRangeAsync(newRedactions, ct);
        }

        await _db.SaveChangesAsync(ct);
        await _audit.RecordAsync(doc.FoiaRequestId, AuditEventType.RedactionCompleted,
            $"Redacted '{doc.FileName}' ({newRedactions.Count} finding(s)).", doc.Id, ct);
        return new CaseSaveRedactionResultsOutput(doc.Id, doc.RedactionStatus.ToString(), newRedactions.Count);
    }

    public virtual async Task<CaseSaveReviewDecisionOutput> CaseSaveReviewDecisionAsync(CaseSaveReviewDecisionInput input, CancellationToken ct = default)
    {
        var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == input.DocumentId, ct)
            ?? throw new KeyNotFoundException($"Document {input.DocumentId} not found.");

        var approved = input.Decision.Equals("Approved", StringComparison.OrdinalIgnoreCase);
        doc.ReviewStatus = approved ? ReviewStatus.Approved : ReviewStatus.ManualHandling;

        await _db.SaveChangesAsync(ct);
        await _audit.RecordAsync(doc.FoiaRequestId,
            approved ? AuditEventType.DocumentApproved : AuditEventType.DocumentRejected,
            approved ? $"Approved '{doc.FileName}'." : $"Rejected '{doc.FileName}': {input.Comments}",
            doc.Id, ct);
        return new CaseSaveReviewDecisionOutput(doc.Id, doc.ReviewStatus.ToString());
    }

    public virtual async Task<CaseSaveReleasePackageOutput> CaseSaveReleasePackageAsync(CaseSaveReleasePackageInput input, CancellationToken ct = default)
    {
        var currentVersion = await _db.ReleasePackages
            .Where(p => p.FoiaRequestId == input.CaseId)
            .Select(p => (int?)p.Version)
            .MaxAsync(ct) ?? 0;
        var pkg = new ReleasePackage
        {
            FoiaRequestId = input.CaseId,
            Version = currentVersion + 1,
            ZipBlobName = input.ZipBlobName,
            BlobContainerName = input.BlobContainerName,
            SasUrl = input.SasUrl,
            SasExpiresAt = input.SasExpiresAt,
            CreatedAt = DateTime.UtcNow,
            Documents = input.Documents.Select(d => new ReleasePackageDocument
            {
                DocumentId = d.DocumentId,
                FileName = d.FileName,
                PackageFileName = d.PackageFileName,
                Included = d.Included,
                ContentSha256 = d.ContentSha256,
            }).ToList(),
        };
        _db.ReleasePackages.Add(pkg);
        await _db.SaveChangesAsync(ct);
        var includedCount = input.Documents.Count(d => d.Included);
        var excludedCount = input.Documents.Count - includedCount;
        await _audit.RecordAsync(input.CaseId, AuditEventType.ReleaseCompositionCreated,
            $"Release package version {pkg.Version} created with {includedCount} included and {excludedCount} excluded document(s).",
            ct: ct);
        await _audit.RecordAsync(input.CaseId, AuditEventType.SasUrlGenerated,
            $"Release package version {pkg.Version} created.", ct: ct);
        return new CaseSaveReleasePackageOutput(pkg.Id);
    }

    private static AuditEventType MapStatusToAuditEventType(RequestStatus status) => status switch
    {
        RequestStatus.Validated => AuditEventType.RequestValidated,
        RequestStatus.Searching => AuditEventType.SearchStarted,
        RequestStatus.DocumentsFound => AuditEventType.SearchCompleted,
        RequestStatus.NoDocumentsFound => AuditEventType.SearchCompleted,
        RequestStatus.Redacting => AuditEventType.RedactionStarted,
        RequestStatus.PendingHumanReview => AuditEventType.HumanReviewStarted,
        RequestStatus.ApprovedForRelease => AuditEventType.ReleaseApproved,
        RequestStatus.Packaging => AuditEventType.PackageCreated,
        RequestStatus.ReleasePackageReady => AuditEventType.SasUrlGenerated,
        RequestStatus.Error => AuditEventType.Error,
        _ => AuditEventType.RequestValidated,
    };
}
