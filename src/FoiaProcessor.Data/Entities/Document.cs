namespace FoiaProcessor.Data.Entities;

public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FoiaRequestId { get; set; }
    public FoiaRequest? FoiaRequest { get; set; }

    public string SourceDocumentId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public string? SourceUri { get; set; }
    public string OriginalContent { get; set; } = string.Empty;
    public string? RedactedContent { get; set; }
    public RedactionStatus RedactionStatus { get; set; } = RedactionStatus.NotStarted;
    public ReviewStatus ReviewStatus { get; set; } = ReviewStatus.NotStarted;
    public bool IncludeInRelease { get; set; } = true;
    public DateTime RetrievedAt { get; set; } = DateTime.UtcNow;

    public List<Redaction> Redactions { get; set; } = new();
}
