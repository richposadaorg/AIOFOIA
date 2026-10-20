namespace FoiaProcessor.Data.Entities;

public class KnowledgeOperation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ResourceId { get; set; }
    public string ResourceType { get; set; } = string.Empty;
    public string OperationType { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string RequestedBy { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public string? TraceId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
