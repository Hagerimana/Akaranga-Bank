#nullable disable

using System.ComponentModel.DataAnnotations;
using AkarangaBankMvc.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AkarangaBankMvc.Web.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ReinitialiserMotDePasseModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ReinitialiserMotDePasseModel> _logger;

        public ReinitialiserMotDePasseModel(
            UserManager<ApplicationUser> userManager,
            ILogger<ReinitialiserMotDePasseModel> logger)
        {
            _userManager = userManager;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Le nouveau mot de passe est obligatoire.")]
            [StringLength(100, MinimumLength = 12,
                ErrorMessage = "Le mot de passe doit contenir entre {2} et {1} caractères.")]
            [DataType(DataType.Password)]
            [Display(Name = "Nouveau mot de passe")]
            public string Password { get; set; }

            [Required(ErrorMessage = "La confirmation du mot de passe est obligatoire.")]
            [DataType(DataType.Password)]
            [Compare(nameof(Password), ErrorMessage = "Les deux mots de passe ne correspondent pas.")]
            [Display(Name = "Confirmer le mot de passe")]
            public string ConfirmPassword { get; set; }
        }

        public IActionResult OnGet()
        {
            if (GetEmailEtToken() is null)
            {
                return RedirectToPage("./MotDePasseOublie");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var contexte = GetEmailEtToken();

            if (contexte is null)
            {
                return RedirectToPage("./MotDePasseOublie");
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var (email, token) = contexte.Value;
            var utilisateur = await _userManager.FindByEmailAsync(email);

            if (utilisateur is null)
            {
                return RedirectToPage("./MotDePasseOublie");
            }

            var resultat = await _userManager.ResetPasswordAsync(
                utilisateur,
                token,
                Input.Password);

            if (!resultat.Succeeded)
            {
                foreach (var erreur in resultat.Errors)
                {
                    ModelState.AddModelError(string.Empty, TraduireErreur(erreur));
                }

                if (resultat.Errors.Any(e => e.Code == "InvalidToken"))
                {
                    TempData.Remove("ResetEmail");
                    TempData.Remove("ResetToken");

                    ModelState.AddModelError(
                        string.Empty,
                        "Le délai de réinitialisation a expiré. Recommencez la procédure.");
                }

                return Page();
            }

            _logger.LogInformation(
                "Mot de passe réinitialisé pour l'utilisateur {UtilisateurId}.",
                utilisateur.Id);

            TempData.Remove("ResetEmail");
            TempData.Remove("ResetToken");
            TempData["SuccessMessage"] =
                "Votre mot de passe a été modifié. Vous pouvez maintenant vous connecter.";

            return RedirectToPage("./Login");
        }

        private (string Email, string Token)? GetEmailEtToken()
        {
            var email = TempData.Peek("ResetEmail") as string;
            var token = TempData.Peek("ResetToken") as string;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            return (email, token);
        }

        private static string TraduireErreur(IdentityError erreur) => erreur.Code switch
        {
            "PasswordTooShort" =>
                "Le mot de passe doit contenir au moins 12 caractères.",
            "PasswordRequiresDigit" =>
                "Le mot de passe doit contenir au moins un chiffre.",
            "PasswordRequiresLower" =>
                "Le mot de passe doit contenir au moins une lettre minuscule.",
            "PasswordRequiresUpper" =>
                "Le mot de passe doit contenir au moins une lettre majuscule.",
            "PasswordRequiresNonAlphanumeric" =>
                "Le mot de passe doit contenir au moins un caractère spécial.",
            "InvalidToken" =>
                "Le lien de réinitialisation n'est plus valide.",
            _ => erreur.Description
        };
    }
}
