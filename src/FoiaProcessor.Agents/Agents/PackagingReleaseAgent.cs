using FoiaProcessor.Agents.Infrastructure;
using FoiaProcessor.Data;
using FoiaProcessor.Data.Entities;
using FoiaProcessor.McpTools.Contracts;
using FoiaProcessor.McpTools.Options;
using FoiaProcessor.McpTools.Servers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FoiaProcessor.Agents.Agents;

/// <summary>
/// Agent 5: Orchestrates the release pipeline (zip → upload → SAS → persist).
/// The pipeline has no decision points, so it runs deterministically without
/// an LLM — this avoids the failure mode where the chat agent stops after
/// the first tool call and leaves the zip in memory.
/// </summary>
public class PackagingReleaseAgent
{
    private readonly CaseServer _case;
    private readonly BlobStorageServer _blob;
    private readonly FoiaDbContext _db;
    private readonly AzureBlobStorageOptions _blobOpts;
    private readonly AgentChatClientFactory _chatFactory;
    private readonly ILogger<PackagingReleaseAgent> _logger;

    public PackagingReleaseAgent(
        CaseServer caseServer,
        BlobStorageServer blobServer,
        FoiaDbContext db,
        IOptions<AzureBlobStorageOptions> blobOpts,
        AgentChatClientFactory chatFactory,
        ILogger<PackagingReleaseAgent> logger)
    {
        _case = caseServer;
        _blob = blobServer;
        _db = db;
        _blobOpts = blobOpts.Value;
        _chatFactory = chatFactory; // retained for DI compatibility; not used here
        _logger = logger;
    }

    public virtual async Task RunAsync(Guid requestId, CancellationToken ct = default)
    {
        await _case.CaseUpdateStatusAsync(
            new CaseUpdateStatusInput(requestId, nameof(RequestStatus.Packaging), "Building release package."),
            ct);

        // 1. Build the zip from approved redacted documents, rendering each as a PDF first.
        var docs = await _db.Documents
            .AsNoTracking()
            .Where(d => d.FoiaRequestId == requestId)
            .Select(d => new
            {
                d.Id,
                d.FileName,
                d.RedactedContent,
                d.RedactionStatus,
                d.ReviewStatus,
                d.IncludeInRelease,
            })
            .OrderBy(d => d.FileName)
            .ThenBy(d => d.Id)
            .ToListAsync(ct);

        var selectedDocs = docs.Where(d => d.IncludeInRelease).ToList();
        if (selectedDocs.Count == 0 || selectedDocs.Any(d =>
                d.ReviewStatus != ReviewStatus.Approved
                || d.RedactionStatus != RedactionStatus.Completed
                || d.RedactedContent is null))
        {
            throw new InvalidOperationException(
                "A release package requires at least one selected document, and every selected document must be redacted and approved.");
        }

        var pdfs = selectedDocs.ToDictionary(
            d => d.Id,
            d => PdfRenderer.Render(d.FileName, d.RedactedContent!));
        var entryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var packageFileNames = new Dictionary<Guid, string>();
        var entries = selectedDocs
            .Select(d =>
            {
                var fileName = CreateUniquePackageFileName(d.FileName, d.Id, entryNames);
                packageFileNames[d.Id] = fileName;
                return new ZipFileEntryBinary(fileName, pdfs[d.Id]);
            })
            .ToList();
        var zip = await _blob.CreateZipPackageBinaryAsync(new CreateZipPackageBinaryInput(requestId, entries), ct);
        _logger.LogInformation(
            "Packaging: built zip with {DocCount} PDF document(s) for request {RequestId}.", entries.Count, requestId);

        // 2. Upload to blob storage.
        var blobName = $"{requestId}/release-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}.zip";
        await _blob.UploadToBlobStorageAsync(
            new UploadToBlobStorageInput(_blobOpts.ContainerName, blobName, zip.ZipBytesBase64, "application/zip"),
            ct);
        _logger.LogInformation(
            "Packaging: uploaded {BlobName} to container '{Container}' for request {RequestId}.",
            blobName, _blobOpts.ContainerName, requestId);

        // 3. Generate a SAS URL.
        var sas = await _blob.GenerateSasUrlAsync(
            new GenerateSasUrlInput(_blobOpts.ContainerName, blobName, _blobOpts.SasExpirationDays),
            ct);
        _logger.LogInformation(
            "Packaging: generated SAS URL (expires {ExpiresAt:O}) for request {RequestId}.",
            sas.ExpiresAt, requestId);

        // 4. Persist the release package and mark the request ready.
        await _case.CaseSaveReleasePackageAsync(
            new CaseSaveReleasePackageInput(
                requestId,
                blobName,
                _blobOpts.ContainerName,
                sas.SasUrl,
                sas.ExpiresAt,
                docs.Select(d => new ReleasePackageDocumentSnapshot(
                    d.Id,
                    d.FileName,
                    d.IncludeInRelease ? packageFileNames[d.Id] : null,
                    d.IncludeInRelease,
                    d.IncludeInRelease ? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pdfs[d.Id])) : null))
                    .ToList()),
            ct);
        await _case.CaseUpdateStatusAsync(
            new CaseUpdateStatusInput(requestId, nameof(RequestStatus.ReleasePackageReady), "Release package ready."),
            ct);
    }

    private static string CreateUniquePackageFileName(string sourceFileName, Guid documentId, ISet<string> usedNames)
    {
        var safeFileName = Path.GetFileName(sourceFileName.Replace('\\', '/'));
        var pdfFileName = Path.ChangeExtension(safeFileName, ".pdf");
        var extension = Path.GetExtension(pdfFileName);
        var name = Path.GetFileNameWithoutExtension(pdfFileName);
        var duplicateIndex = 0;
        while (true)
        {
            var suffix = duplicateIndex == 0
                ? string.Empty
                : $"-{documentId:N}{(duplicateIndex == 1 ? string.Empty : $"-{duplicateIndex}")}";
            var maxNameLength = 300 - extension.Length - suffix.Length;
            var candidate = $"{name[..Math.Min(name.Length, maxNameLength)]}{suffix}{extension}";
            if (usedNames.Add(candidate)) return candidate;
            duplicateIndex++;
        }
    }
}
