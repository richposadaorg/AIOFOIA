namespace FoiaProcessor.Data.Entities;

public class KnowledgeProfileSource
{
    public Guid KnowledgeProfileId { get; set; }
    public KnowledgeProfile? KnowledgeProfile { get; set; }
    public Guid KnowledgeSourceId { get; set; }
    public KnowledgeSource? KnowledgeSource { get; set; }
}
