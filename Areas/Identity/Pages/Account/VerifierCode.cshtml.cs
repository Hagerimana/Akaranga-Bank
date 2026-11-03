#nullable disable

using System.ComponentModel.DataAnnotations;
using AkarangaBankMvc.Web.Models;
using AkarangaBankMvc.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AkarangaBankMvc.Web.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class VerifierCodeModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICodeVerificationService _codes;
        private readonly ILogger<VerifierCodeModel> _logger;

        public VerifierCodeModel(
            UserManager<ApplicationUser> userManager,
            ICodeVerificationService codes,
            ILogger<VerifierCodeModel> logger)
        {
            _userManager = userManager;
            _codes = codes;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public string EmailMasque { get; set; } = string.Empty;

        public class InputModel
        {
            [Required(ErrorMessage = "Le code est obligatoire.")]
            [RegularExpression(@"^\d{6}$", ErrorMessage = "Le code doit contenir exactement 6 chiffres.")]
            [Display(Name = "Code de vérification")]
            public string Code { get; set; }
        }

        public IActionResult OnGet()
        {
            var email = TempData.Peek("CodeEmail") as string;

            if (string.IsNullOrWhiteSpace(email))
            {
                return RedirectToPage("./MotDePasseOublie");
            }

            Input = new InputModel();
            EmailMasque = MasquerEmail(email);

            return Page();
        }

        public async Task<IActionResult> OnPostVerifierAsync()
        {
            var email = TempData.Peek("CodeEmail") as string;

            if (string.IsNullOrWhiteSpace(email))
            {
                return RedirectToPage("./MotDePasseOublie");
            }

            EmailMasque = MasquerEmail(email);

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var utilisateur = await _userManager.FindByEmailAsync(email);

            if (utilisateur is null || !utilisateur.EstActif)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Code invalide ou expiré. Demandez un nouveau code.");
                return Page();
            }

            var resultat = await _codes.VerifierCodeAsync(
                utilisateur,
                ObjectifCode.ReinitialisationMotDePasse,
                Input.Code);

            if (resultat != ResultatVerificationCode.Reussi)
            {
                ModelState.AddModelError(string.Empty, resultat switch
                {
                    ResultatVerificationCode.Expire =>
                        "Ce code a expiré. Demandez un nouveau code.",
                    ResultatVerificationCode.TropDeTentatives =>
                        "Trop de tentatives incorrectes. Demandez un nouveau code.",
                    ResultatVerificationCode.AucunCode =>
                        "Aucun code actif. Demandez un nouveau code.",
                    _ => "Code invalide. Vérifiez les chiffres saisis."
                });

                return Page();
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(utilisateur);

            TempData["ResetEmail"] = email;
            TempData["ResetToken"] = token;
            TempData.Remove("CodeEmail");

            return RedirectToPage("./ReinitialiserMotDePasse");
        }

        public async Task<IActionResult> OnPostRenvoyerAsync()
        {
            var email = TempData.Peek("CodeEmail") as string;

            if (string.IsNullOrWhiteSpace(email))
            {
                return RedirectToPage("./MotDePasseOublie");
            }

            EmailMasque = MasquerEmail(email);

            var utilisateur = await _userManager.FindByEmailAsync(email);

            if (utilisateur is not null && utilisateur.EstActif)
            {
                try
                {
                    await _codes.EnvoyerCodeAsync(
                        utilisateur,
                        ObjectifCode.ReinitialisationMotDePasse);

                    StatusMessage =
                        "Un nouveau code de vérification vient d'être envoyé.";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Échec du renvoi du code à {Email}.", email);

                    ModelState.AddModelError(
                        string.Empty,
                        "Le nouveau code n'a pas pu être envoyé. Réessayez plus tard.");
                }
            }
            else
            {
                StatusMessage =
                    "Si cette adresse est associée à un compte, un nouveau code a été envoyé.";
            }

            Input = new InputModel();
            return Page();
        }

        private static string MasquerEmail(string email)
        {
            var arobase = email.IndexOf('@');

            if (arobase <= 0)
            {
                return "votre adresse email";
            }

            var premiereLettre = email[0];
            var domaine = email[arobase..];

            return $"{premiereLettre}***{domaine}";
        }
    }
}
