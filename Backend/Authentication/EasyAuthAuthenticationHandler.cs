using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Backend.Authentication;

public class EasyAuthAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public EasyAuthAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock)
        : base(options, logger, encoder, clock)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        try
        {
            // Easy Auth passes user info in headers
            var principalHeader = Request.Headers["X-MS-CLIENT-PRINCIPAL"].FirstOrDefault();
            
            if (string.IsNullOrEmpty(principalHeader))
            {
                // No authentication header found
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            // Decode the base64 encoded principal
            var principalBytes = Convert.FromBase64String(principalHeader);
            var principalJson = System.Text.Encoding.UTF8.GetString(principalBytes);
            var principal = JsonSerializer.Deserialize<EasyAuthPrincipal>(principalJson);

            if (principal?.Claims == null || !principal.Claims.Any())
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            // Create claims from Easy Auth principal
            var claims = principal.Claims.Select(c => new Claim(c.Type, c.Value)).ToList();
            
            // Add standard claims if not present
            if (!claims.Any(c => c.Type == ClaimTypes.NameIdentifier))
            {
                var userIdClaim = claims.FirstOrDefault(c => c.Type == "oid" || c.Type == "sub");
                if (userIdClaim != null)
                {
                    claims.Add(new Claim(ClaimTypes.NameIdentifier, userIdClaim.Value));
                }
            }

            if (!claims.Any(c => c.Type == ClaimTypes.Name))
            {
                var nameClaim = claims.FirstOrDefault(c => c.Type == "name");
                if (nameClaim != null)
                {
                    claims.Add(new Claim(ClaimTypes.Name, nameClaim.Value));
                }
            }

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var claimsPrincipal = new ClaimsPrincipal(identity);

            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(claimsPrincipal, Scheme.Name)));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing Easy Auth authentication");
            return Task.FromResult(AuthenticateResult.Fail("Error processing authentication"));
        }
    }
}

public class EasyAuthPrincipal
{
    public string AuthenticationType { get; set; } = "";
    public List<EasyAuthClaim> Claims { get; set; } = new();
    public string NameClaimType { get; set; } = "";
    public string RoleClaimType { get; set; } = "";
}

public class EasyAuthClaim
{
    public string Type { get; set; } = "";
    public string Value { get; set; } = "";
}
