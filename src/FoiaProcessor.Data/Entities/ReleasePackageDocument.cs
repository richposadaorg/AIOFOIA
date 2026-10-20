namespace FoiaProcessor.Data.Entities;

public class ReleasePackageDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReleasePackageId { get; set; }
    public ReleasePackage? ReleasePackage { get; set; }

    public Guid DocumentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? PackageFileName { get; set; }
    public bool Included { get; set; }
    public string? ContentSha256 { get; set; }
}
