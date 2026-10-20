namespace FoiaProcessor.Data.Entities;

public class KnowledgeProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public KnowledgeResourceState State { get; set; } = KnowledgeResourceState.Draft;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int Revision { get; set; } = 1;
    public List<KnowledgeProfileSource> Sources { get; set; } = new();
}
