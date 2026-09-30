namespace FoiaProcessor.Api.Contracts;

public record SubmitFoiaRequestDto(
    string Subject,
    string? Description,
    DateOnly RequestedStartDate,
    DateOnly RequestedEndDate,
    string RequestorFullName,
    string? RequestorOrganization,
    string RequestorEmail,
    string? RequestorPhone,
    string? RequestorMailingAddress);

public record SubmitFoiaRequestResponseDto(
    Guid Id,
    string Status,
    DateTime SubmittedAt);

public record FoiaRequestSummaryDto(
    Guid Id,
    string Subject,
    string RequestorFullName,
    string Status,
    DateTime SubmittedAt);

public record PagedFoiaRequestsDto(
    int Total,
    int Skip,
    int Take,
    IReadOnlyList<FoiaRequestSummaryDto> Items);

public record CountsDto(
    int DocumentsFound,
    int DocumentsPendingReview,
    int DocumentsApproved,
    int DocumentsRejected);

public record ReleaseDto(
    string Status,
    string? SasUrl,
    DateTime? SasExpiresAt);

public record AuditEventDto(
    DateTime Timestamp,
    string EventType,
    string Message,
    Guid? RelatedDocumentId,
    string? RelatedDocumentFileName);

public record FoiaRequestStatusDto(
    Guid Id,
    string Subject,
    string? Description,
    string RequestorFullName,
    string RequestorEmail,
    string? RequestorOrganization,
    string? RequestorPhone,
    string? RequestorMailingAddress,
    DateOnly RequestedStartDate,
    DateOnly RequestedEndDate,
    string Status,
    DateTime SubmittedAt,
    CountsDto Counts,
    ReleaseDto Release,
    IReadOnlyList<AuditEventDto> AuditEvents);

public record DocumentSummaryDto(
    Guid Id,
    string FileName,
    string FileType,
    string RedactionStatus,
    string ReviewStatus,
    int RedactionCount,
    bool IncludeInRelease);

public record DocumentsListDto(Guid RequestId, IReadOnlyList<DocumentSummaryDto> Documents);

public record DocumentRedactionDto(
    Guid Id,
    string PiiType,
    string OriginalText,
    string ReplacementText,
    int? StartOffset,
    int? EndOffset,
    int? PageNumber,
    double? Confidence,
    string DetectionSource,
    bool? ReviewerApproved,
    string? ReviewerComments);

public record DocumentReviewDto(
    Guid Id,
    Guid FoiaRequestId,
    string FileName,
    string OriginalContent,
    string? RedactedContent,
    IReadOnlyList<DocumentRedactionDto> Redactions,
    string ReviewStatus);

public record ApproveDocumentRequestDto(string? Comments);
public record ApproveDocumentResponseDto(Guid Id, string ReviewStatus, DateTime ApprovedAt);

public record RejectDocumentRequestDto(string Comments);
public record RejectDocumentResponseDto(Guid Id, string ReviewStatus, DateTime RejectedAt);
public record CreateRedactionRequestDto(
    string PiiType,
    string OriginalText,
    string ReplacementText,
    int? StartOffset,
    int? EndOffset,
    int? PageNumber);
public record UpdateRedactionRequestDto(string ReplacementText, string? ReviewerComments);
public record ReleaseSelectionRequestDto(bool IncludeInRelease);

public record ApproveReleaseResponseDto(Guid Id, string Status, DateTime ApprovedAt);

public record ReleasePackageDto(
    Guid RequestId,
    string Status,
    string? ZipBlobName,
    string? BlobContainerName,
    string? SasUrl,
    DateTime? SasExpiresAt,
    DateTime? CreatedAt);
