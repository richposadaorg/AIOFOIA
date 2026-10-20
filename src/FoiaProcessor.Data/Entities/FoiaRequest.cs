namespace FoiaProcessor.Data.Entities;

public class FoiaRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly RequestedStartDate { get; set; }
    public DateOnly RequestedEndDate { get; set; }
    public string RequestorFullName { get; set; } = string.Empty;
    public string? RequestorOrganization { get; set; }
    public string RequestorEmail { get; set; } = string.Empty;
    public string? RequestorPhone { get; set; }
    public string? RequestorMailingAddress { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public RequestStatus Status { get; set; } = RequestStatus.Submitted;

    public List<Document> Documents { get; set; } = new();
    public List<ReviewTask> ReviewTasks { get; set; } = new();
    public List<ReleasePackage> ReleasePackages { get; set; } = new();
    public List<AuditEvent> AuditEvents { get; set; } = new();
}
