namespace FoiaProcessor.McpTools.Contracts;

// case_create
public record CaseCreateInput(
    string Subject,
    string? Description,
    DateOnly RequestedStartDate,
    DateOnly RequestedEndDate,
    string RequestorFullName,
    string? RequestorOrganization,
    string RequestorEmail,
    string? RequestorPhone,
    string? RequestorMailingAddress);

public record CaseCreateOutput(Guid CaseId, string Status);

// case_get
public record CaseGetInput(Guid CaseId);
public record CaseGetOutput(
    Guid CaseId,
    string Status,
    string Subject,
    string RequestorFullName,
    DateTime SubmittedAt);

// case_update_status
public record CaseUpdateStatusInput(Guid CaseId, string NewStatus, string Message);
public record CaseUpdateStatusOutput(Guid CaseId, string Status, Guid AuditEventId);

// case_add_note
public record CaseAddNoteInput(Guid CaseId, string Message, Guid? RelatedDocumentId = null);
public record CaseAddNoteOutput(Guid AuditEventId);

// case_save_document_metadata
public record SaveDocumentInput(
    string SourceDocumentId,
    string FileName,
    string FileType,
    string? SourceUri,
    string OriginalContent);

public record CaseSaveDocumentMetadataInput(Guid CaseId, IReadOnlyList<SaveDocumentInput> Documents);
public record CaseSaveDocumentMetadataOutput(IReadOnlyList<Guid> SavedDocumentIds);

// case_save_redaction_results
public record RedactionFinding(
    string PiiType,
    string OriginalText,
    string ReplacementText,
    int? StartOffset,
    int? EndOffset,
    int? PageNumber,
    double? Confidence,
    string DetectionSource);

public record CaseSaveRedactionResultsInput(
    Guid DocumentId,
    string RedactedContent,
    IReadOnlyList<RedactionFinding> Redactions);

public record CaseSaveRedactionResultsOutput(Guid DocumentId, string RedactionStatus, int RedactionCount);

// case_save_review_decision
public record CaseSaveReviewDecisionInput(Guid DocumentId, string Decision, string? Comments);
public record CaseSaveReviewDecisionOutput(Guid DocumentId, string ReviewStatus);

// case_save_release_package
public record CaseSaveReleasePackageInput(
    Guid CaseId,
    string ZipBlobName,
    string BlobContainerName,
    string SasUrl,
    DateTime SasExpiresAt,
    IReadOnlyList<ReleasePackageDocumentSnapshot> Documents);

public record ReleasePackageDocumentSnapshot(
    Guid DocumentId,
    string FileName,
    string? PackageFileName,
    bool Included,
    string? ContentSha256);

public record CaseSaveReleasePackageOutput(Guid PackageId);
