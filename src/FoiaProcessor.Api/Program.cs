using DotNetEnv;
using FoiaProcessor.Agents;
using FoiaProcessor.Data;
using FoiaProcessor.Data.Audit;
using FoiaProcessor.McpTools.Hosting;
using FoiaProcessor.McpTools.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Identity.Web;
using Microsoft.OpenApi;
using Serilog;

// Load .env from the project directory if present (local dev convenience).
Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// -------- Serilog (T017) --------
builder.Host.UseSerilog((ctx, services, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.Debug(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj} {Properties:j}{NewLine}{Exception}"));

// -------- Options binding (T014) --------
builder.Services.Configure<AzureOpenAIOptions>(builder.Configuration.GetSection(AzureOpenAIOptions.SectionName));
builder.Services.Configure<AzureSearchOptions>(builder.Configuration.GetSection(AzureSearchOptions.SectionName));
builder.Services.Configure<AzureBlobStorageOptions>(builder.Configuration.GetSection(AzureBlobStorageOptions.SectionName));
builder.Services.Configure<WorkflowOptions>(builder.Configuration.GetSection(WorkflowOptions.SectionName));

// -------- Data + audit --------
builder.Services.AddFoiaData(builder.Configuration);
builder.Services.AddScoped<IAuditWriter, AuditWriter>();

// -------- MCP tools --------
builder.Services.AddFoiaMcpTools();

// -------- Agents + workflow --------
builder.Services.AddFoiaAgents();

// -------- Health probes --------
builder.Services.AddScoped<FoiaProcessor.Api.Services.HealthProbeService>();

// -------- Web --------
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste an Entra ID access token (without the 'Bearer ' prefix).",
    });
    o.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference("Bearer"), new List<string>() },
    });
});

// -------- Entra ID authentication --------
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
builder.Services.AddAuthorization(options =>
{
    static bool HasAppRole(System.Security.Claims.ClaimsPrincipal user, string role) =>
        user.Claims.Any(claim =>
            (claim.Type == "roles"
             || claim.Type == "role"
             || claim.Type == System.Security.Claims.ClaimTypes.Role)
            && string.Equals(claim.Value, role, StringComparison.Ordinal));

    options.AddPolicy("FoiaAdmin", policy =>
        policy.RequireAuthenticatedUser()
            .RequireAssertion(context => HasAppRole(context.User, "Foia.Admin")));
    options.AddPolicy("FoiaAnalyst", policy =>
        policy.RequireAuthenticatedUser()
            .RequireAssertion(context => HasAppRole(context.User, "Foia.Analyst")));
});

var app = builder.Build();

// -------- Database initialization (T025) --------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FoiaDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();

app.UseExceptionHandler(errApp => errApp.Run(async ctx =>
{
    ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
    ctx.Response.ContentType = "application/json";
    var traceId = System.Diagnostics.Activity.Current?.Id ?? ctx.TraceIdentifier;
    var feature = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
    var detail = feature?.Error.Message ?? "Internal server error.";
    var payload = System.Text.Json.JsonSerializer.Serialize(new
    {
        title = "Internal server error.",
        status = 500,
        detail,
        traceId,
    });
    await ctx.Response.WriteAsync(payload);
}));

// -------- Static files / SPA fallback --------
var wwwroot = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
if (Directory.Exists(wwwroot))
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// SPA fallback to index.html (only when wwwroot exists).
if (Directory.Exists(wwwroot))
{
    app.MapFallbackToFile("index.html", new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(wwwroot),
    });
}

app.Run();

public partial class Program { }
