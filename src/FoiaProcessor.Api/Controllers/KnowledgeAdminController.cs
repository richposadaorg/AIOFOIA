using FoiaProcessor.Data;
using FoiaProcessor.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoiaProcessor.Api.Controllers;

[ApiController]
[Authorize(Policy = "FoiaAdmin")]
[Route("api/admin")]
public class KnowledgeAdminController : ControllerBase
{
    private readonly FoiaDbContext _db;

    public KnowledgeAdminController(FoiaDbContext db) => _db = db;

    [HttpGet("knowledge-source-types")]
    public IActionResult GetSourceTypes() => Ok(SourceTypes);

    [HttpGet("knowledge-sources")]
    public async Task<IActionResult> ListSources(CancellationToken ct)
    {
        var sources = await _db.KnowledgeSources.AsNoTracking()
            .OrderBy(source => source.Name)
            .Select(source => new KnowledgeSourceDto(
                source.Id,
                source.Kind,
                source.Name,
                source.Description,
                source.State,
                source.CapabilityState,
                source.Revision,
                source.CreatedAt,
                source.UpdatedAt))
            .ToListAsync(ct);
        return Ok(sources);
    }

    [HttpGet("knowledge-sources/{id:guid}")]
    public async Task<IActionResult> GetSource(Guid id, CancellationToken ct)
    {
        var source = await _db.KnowledgeSources.AsNoTracking()
            .FirstOrDefaultAsync(source => source.Id == id, ct);
        return source is null ? NotFound() : Ok(ToDto(source));
    }

    [HttpPost("knowledge-sources")]
    public async Task<IActionResult> CreateSource(CreateKnowledgeSourceDto dto, CancellationToken ct)
    {
        if (!Enum.IsDefined(dto.Kind)
            || string.IsNullOrWhiteSpace(dto.Name)
            || dto.Name.Length > 200
            || (dto.Description?.Length ?? 0) > 1000)
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["source"] = ["A supported source type, a name of at most 200 characters, and a description of at most 1000 characters are required."],
            }));
        }

        if (await _db.KnowledgeSources.AnyAsync(source => source.Name == dto.Name.Trim(), ct))
        {
            return Conflict(new { title = "Source name already exists.", status = 409 });
        }

        var now = DateTime.UtcNow;
        var source = new KnowledgeSource
        {
            Kind = dto.Kind,
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim() ?? string.Empty,
            CreatedBy = Actor,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.KnowledgeSources.Add(source);
        AddAudit("CreateDraft", "Source", source.Id, source.Revision, "Succeeded");
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetSource), new { id = source.Id }, ToDto(source));
    }

    [HttpPut("knowledge-sources/{id:guid}")]
    public async Task<IActionResult> UpdateSource(Guid id, UpdateKnowledgeSourceDto dto, CancellationToken ct)
    {
        var source = await _db.KnowledgeSources.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (source is null) return NotFound();
        if (source.State != KnowledgeResourceState.Draft)
            return Conflict(new { title = "Only draft sources can be edited.", status = 409 });
        if (dto.Revision != source.Revision)
            return Conflict(new { title = "The source has changed. Reload before editing.", status = 409 });
        if (string.IsNullOrWhiteSpace(dto.Name)
            || dto.Name.Length > 200
            || (dto.Description?.Length ?? 0) > 1000)
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["source"] = ["A name of at most 200 characters and a description of at most 1000 characters are required."],
            }));
        }

        var normalizedName = dto.Name.Trim();
        if (await _db.KnowledgeSources.AnyAsync(item => item.Id != id && item.Name == normalizedName, ct))
            return Conflict(new { title = "Source name already exists.", status = 409 });

        source.Name = normalizedName;
        source.Description = dto.Description?.Trim() ?? string.Empty;
        source.Revision++;
        source.UpdatedAt = DateTime.UtcNow;
        AddAudit("UpdateDraft", "Source", source.Id, source.Revision, "Succeeded");
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(source));
    }

    [HttpPost("knowledge-sources/{id:guid}/activate")]
    public async Task<IActionResult> ActivateSource(Guid id, CancellationToken ct)
    {
        var source = await _db.KnowledgeSources.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (source is null) return NotFound();

        AddAudit("Activate", "Source", source.Id, source.Revision, "RejectedUnavailable");
        await _db.SaveChangesAsync(ct);
        return Conflict(new
        {
            title = "Source activation is unavailable.",
            status = 409,
            detail = "No Foundry IQ connector has been verified for this deployment. Sources remain drafts.",
        });
    }

    [HttpPost("knowledge-sources/{id:guid}/disable")]
    public async Task<IActionResult> DisableSource(Guid id, CancellationToken ct)
    {
        var source = await _db.KnowledgeSources.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (source is null) return NotFound();
        source.State = KnowledgeResourceState.Disabled;
        source.CapabilityState = KnowledgeCapabilityState.Disabled;
        source.Revision++;
        source.UpdatedAt = DateTime.UtcNow;
        AddAudit("Disable", "Source", source.Id, source.Revision, "Succeeded");
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(source));
    }

    [HttpDelete("knowledge-sources/{id:guid}")]
    public async Task<IActionResult> DeleteDraftSource(Guid id, CancellationToken ct)
    {
        var source = await _db.KnowledgeSources.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (source is null) return NotFound();
        if (source.State != KnowledgeResourceState.Draft)
            return Conflict(new { title = "Only draft sources can be removed.", status = 409 });
        if (await _db.KnowledgeProfileSources.AnyAsync(link => link.KnowledgeSourceId == id, ct))
            return Conflict(new { title = "Remove profile references before removing this source.", status = 409 });

        AddAudit("DeleteDraft", "Source", source.Id, source.Revision, "Succeeded");
        _db.KnowledgeSources.Remove(source);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("knowledge-profiles")]
    public async Task<IActionResult> ListProfiles(CancellationToken ct)
    {
        var profiles = await _db.KnowledgeProfiles.AsNoTracking()
            .OrderBy(profile => profile.Name)
            .Select(profile => new KnowledgeProfileDto(
                profile.Id,
                profile.Name,
                profile.Description,
                profile.State,
                profile.Revision,
                profile.CreatedAt))
            .ToListAsync(ct);
        return Ok(profiles);
    }

    [HttpPost("knowledge-profiles")]
    public async Task<IActionResult> CreateProfile(CreateKnowledgeProfileDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)
            || dto.Name.Length > 200
            || (dto.Description?.Length ?? 0) > 1000)
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["profile"] = ["A name of at most 200 characters and a description of at most 1000 characters are required."],
            }));
        }

        var normalizedName = dto.Name.Trim();
        if (await _db.KnowledgeProfiles.AnyAsync(profile => profile.Name == normalizedName, ct))
            return Conflict(new { title = "Profile name already exists.", status = 409 });

        var profile = new KnowledgeProfile
        {
            Name = normalizedName,
            Description = dto.Description?.Trim() ?? string.Empty,
            CreatedBy = Actor,
        };
        _db.KnowledgeProfiles.Add(profile);
        AddAudit("CreateDraft", "Profile", profile.Id, profile.Revision, "Succeeded");
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(ListProfiles), null, ToDto(profile));
    }

    [HttpGet("knowledge-operations")]
    public async Task<IActionResult> ListOperations([FromQuery] int take = 50, CancellationToken ct = default)
    {
        var operations = await _db.KnowledgeOperations.AsNoTracking()
            .OrderByDescending(operation => operation.CreatedAt)
            .Take(Math.Clamp(take, 1, 100))
            .Select(operation => new KnowledgeOperationDto(
                operation.Id,
                operation.ResourceId,
                operation.ResourceType,
                operation.OperationType,
                operation.Status,
                operation.ErrorCode,
                operation.TraceId,
                operation.CreatedAt,
                operation.UpdatedAt))
            .ToListAsync(ct);
        return Ok(operations);
    }

    private string Actor =>
        User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("oid")?.Value
        ?? "unknown";

    private void AddAudit(string operation, string resourceType, Guid? resourceId, int? revision, string outcome) =>
        _db.KnowledgeAdminAudits.Add(new KnowledgeAdminAudit
        {
            Actor = Actor,
            Operation = operation,
            ResourceType = resourceType,
            ResourceId = resourceId,
            Revision = revision,
            Outcome = outcome,
            TraceId = HttpContext.TraceIdentifier,
        });

    private static KnowledgeSourceDto ToDto(KnowledgeSource source) =>
        new(source.Id, source.Kind, source.Name, source.Description, source.State,
            source.CapabilityState, source.Revision, source.CreatedAt, source.UpdatedAt);

    private static KnowledgeProfileDto ToDto(KnowledgeProfile profile) =>
        new(profile.Id, profile.Name, profile.Description, profile.State, profile.Revision, profile.CreatedAt);

    private static readonly KnowledgeSourceTypeDto[] SourceTypes =
    [
        new(KnowledgeSourceKind.SearchIndex, "Search index", false, "Complete-record import is not yet verified."),
        new(KnowledgeSourceKind.AzureBlob, "Azure Blob / ADLS Gen2", false, "Identity, indexing, permissions, and import are not yet verified."),
        new(KnowledgeSourceKind.AzureSql, "Azure SQL", true, "Preview connector is not yet implemented or verified."),
        new(KnowledgeSourceKind.FileUpload, "File upload", true, "Bounded upload and original retention are not yet implemented."),
        new(KnowledgeSourceKind.OneLake, "OneLake", false, "File synchronization and original export are not yet verified."),
        new(KnowledgeSourceKind.IndexedSharePoint, "Indexed SharePoint", true, "Tenant alignment, consent, and ACL propagation are not yet verified."),
        new(KnowledgeSourceKind.RemoteSharePoint, "Remote SharePoint", true, "Delegated retrieval and separate original acquisition are not yet verified."),
        new(KnowledgeSourceKind.FabricDataAgent, "Fabric Data Agent", true, "Analytical context is not an importable record."),
        new(KnowledgeSourceKind.FabricOntology, "Fabric Ontology", true, "Entity answers are not an importable record."),
        new(KnowledgeSourceKind.McpServer, "MCP server", true, "Read-only tool enforcement and full-record retrieval are not yet verified."),
        new(KnowledgeSourceKind.WorkIq, "Work IQ", true, "Activation is blocked until read-only behavior is verified."),
        new(KnowledgeSourceKind.Web, "Web", false, "Public-context queries and domain restrictions are not yet implemented."),
    ];
}

public sealed record KnowledgeSourceTypeDto(
    KnowledgeSourceKind Kind,
    string DisplayName,
    bool Preview,
    string Availability);

public sealed record CreateKnowledgeSourceDto(KnowledgeSourceKind Kind, string Name, string? Description);
public sealed record UpdateKnowledgeSourceDto(string Name, string? Description, int Revision);
public sealed record CreateKnowledgeProfileDto(string Name, string? Description);
public sealed record KnowledgeSourceDto(
    Guid Id,
    KnowledgeSourceKind Kind,
    string Name,
    string Description,
    KnowledgeResourceState State,
    KnowledgeCapabilityState CapabilityState,
    int Revision,
    DateTime CreatedAt,
    DateTime UpdatedAt);
public sealed record KnowledgeProfileDto(
    Guid Id,
    string Name,
    string Description,
    KnowledgeResourceState State,
    int Revision,
    DateTime CreatedAt);
public sealed record KnowledgeOperationDto(
    Guid Id,
    Guid ResourceId,
    string ResourceType,
    string OperationType,
    string Status,
    string? ErrorCode,
    string? TraceId,
    DateTime CreatedAt,
    DateTime UpdatedAt);
