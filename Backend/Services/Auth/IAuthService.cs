using System.Security.Claims;

namespace Backend.Services.Auth;

public interface IAuthService
{
    Task<bool> ValidateTokenAsync(string token);
    string GetUserIdFromClaims(ClaimsPrincipal user);
    string GetUserEmailFromClaims(ClaimsPrincipal user);
    bool IsUserAuthorized(ClaimsPrincipal user, string resource);
}
