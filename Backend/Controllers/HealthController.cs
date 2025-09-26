using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;

    public HealthController(ILogger<HealthController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public ActionResult GetHealth()
    {
        return Ok(new { 
            status = "healthy", 
            timestamp = DateTime.UtcNow,
            environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown"
        });
    }

    [HttpGet("auth")]
    [Authorize(Policy = "RequireDomainUser")]
    public ActionResult GetAuthenticatedHealth()
    {
        try
        {
            var user = HttpContext.User;
            var claims = user.Claims.Select(c => new { c.Type, c.Value }).ToList();
            
            _logger.LogInformation("Authenticated user accessing health endpoint");
            
            return Ok(new { 
                status = "authenticated", 
                timestamp = DateTime.UtcNow,
                user = new {
                    identity = user.Identity?.Name,
                    isAuthenticated = user.Identity?.IsAuthenticated,
                    authenticationType = user.Identity?.AuthenticationType,
                    claims = claims
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in authenticated health endpoint");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("msal-config")]
    [AllowAnonymous]
    public ActionResult GetMsalConfig()
    {
        var tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
        var clientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
        var domain = Environment.GetEnvironmentVariable("AZURE_DOMAIN");
        
        return Ok(new {
            msalConfiguration = new {
                tenantId = string.IsNullOrEmpty(tenantId) ? "NOT SET" : "***SET***",
                clientId = string.IsNullOrEmpty(clientId) ? "NOT SET" : "***SET***",
                clientSecret = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET")) ? "NOT SET" : "***SET***",
                domain = string.IsNullOrEmpty(domain) ? "NOT SET" : "***SET***"
            },
            timestamp = DateTime.UtcNow
        });
    }
}
