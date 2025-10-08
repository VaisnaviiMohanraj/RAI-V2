using System.Security.Claims;
using Microsoft.Identity.Web;

namespace Backend.Services.Auth;

public class AuthService : IAuthService
{
    private readonly ILogger<AuthService> _logger;

    public AuthService(ILogger<AuthService> logger)
    {
        _logger = logger;
    }

    public Task<bool> ValidateTokenAsync(string token)
    {
        try
        {
            // Token validation is handled by Microsoft.Identity.Web middleware
            // This method can be extended for additional custom validation
            return Task.FromResult(!string.IsNullOrEmpty(token));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token validation failed");
            return Task.FromResult(false);
        }
    }

    public string GetUserIdFromClaims(ClaimsPrincipal user)
    {
        // For anonymous users (AllowAnonymous endpoints), return "anonymous"
        if (user?.Identity?.IsAuthenticated != true)
        {
            return "anonymous";
        }
        return user.GetObjectId() ?? "anonymous";
    }

    public string GetUserEmailFromClaims(ClaimsPrincipal user)
    {
        return user.GetLoginHint() ?? user.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;
    }

    public bool IsUserAuthorized(ClaimsPrincipal user, string resource)
    {
        // Basic authorization - can be extended with role-based access control
        return user.Identity?.IsAuthenticated == true;
    }
}
