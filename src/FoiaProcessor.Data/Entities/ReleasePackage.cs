namespace FoiaProcessor.Data.Entities;

public class ReleasePackage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FoiaRequestId { get; set; }
    public FoiaRequest? FoiaRequest { get; set; }

    public int Version { get; set; }
    public string ZipBlobName { get; set; } = string.Empty;
    public string BlobContainerName { get; set; } = string.Empty;
    public string SasUrl { get; set; } = string.Empty;
    public DateTime SasExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<ReleasePackageDocument> Documents { get; set; } = new();
}
