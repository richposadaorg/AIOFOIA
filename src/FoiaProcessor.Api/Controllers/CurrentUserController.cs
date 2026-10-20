using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoiaProcessor.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/me")]
public class CurrentUserController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var roles = User.Claims
            .Where(claim =>
                claim.Type == "roles"
                || claim.Type == "role"
                || claim.Type == System.Security.Claims.ClaimTypes.Role)
            .Select(claim => claim.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return Ok(new
        {
            roles,
            capabilities = new
            {
                manageKnowledgeSources = roles.Contains("Foia.Admin", StringComparer.Ordinal),
                discoverDocuments = roles.Contains("Foia.Analyst", StringComparer.Ordinal),
            },
        });
    }
}
