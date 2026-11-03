#nullable disable

using System.Text;
using AkarangaBankMvc.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace AkarangaBankMvc.Web.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ConfirmerEmailModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ConfirmerEmailModel> _logger;

        public ConfirmerEmailModel(
            UserManager<ApplicationUser> userManager,
            ILogger<ConfirmerEmailModel> logger)
        {
            _userManager = userManager;
            _logger = logger;
        }

        public bool Succes { get; set; }

        public string Email { get; set; } = string.Empty;

        public string Message { get; set; } =
            "Confirmation de votre adresse email en cours…";

        public async Task<IActionResult> OnGetAsync(int userId, string code)
        {
            if (userId <= 0 || string.IsNullOrWhiteSpace(code))
            {
                Message = "Le lien de confirmation est incomplet ou invalide.";
                return Page();
            }

            var utilisateur = await _userManager.FindByIdAsync(userId.ToString());

            if (utilisateur is null)
            {
                Message = "Le compte associé à ce lien est introuvable.";
                return Page();
            }

            Email = utilisateur.Email ?? string.Empty;

            if (utilisateur.EmailConfirmed)
            {
                Succes = true;
                return Page();
            }

            string token;

            try
            {
                token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            }
            catch (FormatException)
            {
                Message = "Le lien de confirmation est invalide.";
                return Page();
            }

            var resultat = await _userManager.ConfirmEmailAsync(utilisateur, token);

            if (resultat.Succeeded)
            {
                Succes = true;
                _logger.LogInformation(
                    "Adresse email confirmée pour l'utilisateur {UtilisateurId}.",
                    utilisateur.Id);
                return Page();
            }

            Message = "Le lien de confirmation a expiré ou a déjà été utilisé. " +
                "Connectez-vous puis demandez un nouveau lien depuis « Mon compte > Email ».";

            _logger.LogWarning(
                "Échec de confirmation d'email pour {UtilisateurId} : {Erreurs}",
                utilisateur.Id,
                string.Join(" | ", resultat.Errors.Select(e => e.Description)));

            return Page();
        }
    }
}
