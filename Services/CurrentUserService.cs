using AkarangaBankMvc.Web.Models;
using Microsoft.AspNetCore.Identity;

namespace AkarangaBankMvc.Web.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _accessor;
    private readonly UserManager<ApplicationUser> _userManager;

    public CurrentUserService(
        IHttpContextAccessor accessor,
        UserManager<ApplicationUser> userManager)
    {
        _accessor = accessor;
        _userManager = userManager;
    }

    public async Task<int> GetUserIdAsync()
    {
        var user = await GetCurrentUserAsync()
            ?? throw new InvalidOperationException(
                "Utilisateur non connecté.");

        return user.Id;
    }

    public async Task<int> GetAgenceIdAsync()
    {
        var user = await GetCurrentUserAsync()
            ?? throw new InvalidOperationException(
                "Utilisateur non connecté.");

        return user.AgenceId;
    }

    private async Task<ApplicationUser?> GetCurrentUserAsync()
    {
        var httpContext = _accessor.HttpContext;

        if (httpContext?.User is null ||
            !httpContext.User.Identity?.IsAuthenticated == true)
        {
            return null;
        }

        return await _userManager.GetUserAsync(httpContext.User);
    }
}