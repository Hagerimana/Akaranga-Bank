using AkarangaBankMvc.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace AkarangaBankMvc.Web.Services;

public class RequireMfa : IAuthorizationRequirement
{
}

/// <summary>
/// Exige que l'utilisateur ait réellement passé la double authentification.
/// Un utilisateur dont le compte a la 2FA activée ne peut obtenir de cookie
/// qu'après avoir saisi un code TOTP ou un code de secours ; la présence
/// d'une session authentifiée suffit donc pour ces comptes.
/// </summary>
public class RequireMfaHandler : AuthorizationHandler<RequireMfa>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public RequireMfaHandler(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RequireMfa requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        // Certains fournisseurs ajoutent explicitement la méthode "mfa".
        var amrClaim = context.User.FindFirst(c => c.Type == "amr");

        if (amrClaim?.Value == "mfa")
        {
            context.Succeed(requirement);
            return;
        }

        var utilisateur = await _userManager.GetUserAsync(context.User);

        if (utilisateur is null)
        {
            return;
        }

        if (await _userManager.GetTwoFactorEnabledAsync(utilisateur))
        {
            context.Succeed(requirement);
        }
    }
}
