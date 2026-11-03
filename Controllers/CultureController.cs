using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace AkarangaBankMvc.Web.Controllers;

[AllowAnonymous]
public class CultureController : Controller
{
    private static readonly HashSet<string> SupportedCultures = new(StringComparer.OrdinalIgnoreCase)
    {
        "fr-BI", "fr",
        "rn-BI", "rn",
        "en-US", "en",
        "sw-KE", "sw"
    };

    [AcceptVerbs("GET", "POST")]
    [IgnoreAntiforgeryToken]
    public IActionResult SetLanguage(string culture, string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(culture))
        {
            culture = "fr-BI";
        }
        else if (culture.StartsWith("rn", StringComparison.OrdinalIgnoreCase))
        {
            culture = "rn-BI";
        }
        else if (culture.StartsWith("sw", StringComparison.OrdinalIgnoreCase))
        {
            culture = "sw-KE";
        }
        else if (culture.StartsWith("en", StringComparison.OrdinalIgnoreCase))
        {
            culture = "en-US";
        }
        else if (culture.StartsWith("fr", StringComparison.OrdinalIgnoreCase))
        {
            culture = "fr-BI";
        }
        else if (!SupportedCultures.Contains(culture))
        {
            culture = "fr-BI";
        }

        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                HttpOnly = false
            });

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        var referer = Request.Headers["Referer"].ToString();
        if (!string.IsNullOrEmpty(referer) && Uri.TryCreate(referer, UriKind.Absolute, out var refererUri))
        {
            if (string.Equals(refererUri.Host, Request.Host.Host, StringComparison.OrdinalIgnoreCase))
            {
                return Redirect(referer);
            }
        }

        return RedirectToAction("Index", "Home");
    }
}
