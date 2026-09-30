namespace FoiaProcessor.Data.Entities;

public enum RequestStatus
{
    Submitted,
    Validated,
    Searching,
    DocumentsFound,
    NoDocumentsFound,
    Redacting,
    PendingHumanReview,
    ApprovedForRelease,
    Packaging,
    ReleasePackageReady,
    Rejected,
    Error,
}

public enum RedactionStatus
{
    NotStarted,
    InProgress,
    Completed,
    Failed,
}

public enum ReviewStatus
{
    NotStarted,
    Pending,
    Approved,
    ManualHandling,
}

public enum ReviewTaskStatus
{
    Open,
    Completed,
}

public enum ReviewerDecision
{
    Approved,
    Rejected,
}

public enum PiiType
{
    Email,
    Phone,
    SSN,
    DateOfBirth,
    Name,
    Address,
    FinancialId,
}

public enum DetectionSource
{
    Regex,
    Ai,
}

public enum AuditEventType
{
    RequestSubmitted,
    RequestValidated,
    SearchStarted,
    SearchCompleted,
    DocumentsFound,
    RedactionStarted,
    RedactionCompleted,
    HumanReviewStarted,
    DocumentApproved,
    DocumentRejected,
    RedactionAdded,
    RedactionUpdated,
    RedactionRemoved,
    DocumentReleaseSelectionChanged,
    ReleaseApproved,
    PackageCreated,
    SasUrlGenerated,
    Error,
}
