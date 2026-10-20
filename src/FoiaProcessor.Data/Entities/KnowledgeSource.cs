namespace FoiaProcessor.Data.Entities;

public class KnowledgeSource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public KnowledgeSourceKind Kind { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public KnowledgeResourceState State { get; set; } = KnowledgeResourceState.Draft;
    public KnowledgeCapabilityState CapabilityState { get; set; } = KnowledgeCapabilityState.Unavailable;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int Revision { get; set; } = 1;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
