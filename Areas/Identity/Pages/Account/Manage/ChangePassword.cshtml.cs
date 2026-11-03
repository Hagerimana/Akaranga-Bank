#nullable disable

using System.ComponentModel.DataAnnotations;
using AkarangaBankMvc.Web.Models;
using AkarangaBankMvc.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AkarangaBankMvc.Web.Areas.Identity.Pages.Account.Manage
{
    public class ChangePasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ICodeVerificationService _codes;
        private readonly ILogger<ChangePasswordModel> _logger;

        public ChangePasswordModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ICodeVerificationService codes,
            ILogger<ChangePasswordModel> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _codes = codes;
            _logger = logger;
        }

        public string EmailMasque { get; set; } = string.Empty;

        [TempData]
        public string StatusMessage { get; set; }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Le code de vérification est obligatoire.")]
            [RegularExpression(@"^\d{6}$", ErrorMessage = "Le code doit contenir exactement 6 chiffres.")]
            [Display(Name = "Code de vérification")]
            public string Code { get; set; }

            [Required(ErrorMessage = "Le nouveau mot de passe est obligatoire.")]
            [StringLength(100, MinimumLength = 12,
                ErrorMessage = "Le mot de passe doit contenir entre {2} et {1} caractères.")]
            [DataType(DataType.Password)]
            [Display(Name = "Nouveau mot de passe")]
            public string NouveauMotDePasse { get; set; }

            [Required(ErrorMessage = "La confirmation du mot de passe est obligatoire.")]
            [DataType(DataType.Password)]
            [Compare(nameof(NouveauMotDePasse), ErrorMessage = "Les deux mots de passe ne correspondent pas.")]
            [Display(Name = "Confirmer le nouveau mot de passe")]
            public string ConfirmationMotDePasse { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var utilisateur = await _userManager.GetUserAsync(User);

            if (utilisateur is null)
            {
                return NotFound($"Impossible de charger l'utilisateur '{_userManager.GetUserId(User)}'.");
            }

            EmailMasque = MasquerEmail(utilisateur.Email);
            Input = new InputModel();

            return Page();
        }

        public async Task<IActionResult> OnPostEnvoyerCodeAsync()
        {
            var utilisateur = await _userManager.GetUserAsync(User);

            if (utilisateur is null)
            {
                return NotFound($"Impossible de charger l'utilisateur '{_userManager.GetUserId(User)}'.");
            }

            EmailMasque = MasquerEmail(utilisateur.Email);

            try
            {
                await _codes.EnvoyerCodeAsync(
                    utilisateur,
                    ObjectifCode.ChangementMotDePasse);

                StatusMessage = "Un code de vérification vient d'être envoyé à votre adresse email.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Échec de l'envoi du code de changement de mot de passe.");

                ModelState.AddModelError(
                    string.Empty,
                    "Le code n'a pas pu être envoyé. Vérifiez la configuration email et réessayez.");
            }

            Input ??= new InputModel();
            return Page();
        }

        public async Task<IActionResult> OnPostChangerAsync()
        {
            var utilisateur = await _userManager.GetUserAsync(User);

            if (utilisateur is null)
            {
                return NotFound($"Impossible de charger l'utilisateur '{_userManager.GetUserId(User)}'.");
            }

            EmailMasque = MasquerEmail(utilisateur.Email);

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var verification = await _codes.VerifierCodeAsync(
                utilisateur,
                ObjectifCode.ChangementMotDePasse,
                Input.Code);

            if (verification != ResultatVerificationCode.Reussi)
            {
                ModelState.AddModelError(string.Empty, verification switch
                {
                    ResultatVerificationCode.Expire =>
                        "Ce code a expiré. Demandez un nouveau code.",
                    ResultatVerificationCode.TropDeTentatives =>
                        "Trop de tentatives incorrectes. Demandez un nouveau code.",
                    ResultatVerificationCode.AucunCode =>
                        "Aucun code actif. Demandez d'abord un code de vérification.",
                    _ => "Code de vérification invalide."
                });

                return Page();
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(utilisateur);
            var resultat = await _userManager.ResetPasswordAsync(
                utilisateur,
                token,
                Input.NouveauMotDePasse);

            if (!resultat.Succeeded)
            {
                foreach (var erreur in resultat.Errors)
                {
                    ModelState.AddModelError(string.Empty, TraduireErreur(erreur));
                }

                return Page();
            }

            await _signInManager.RefreshSignInAsync(utilisateur);

            _logger.LogInformation(
                "Mot de passe changé par l'utilisateur {UtilisateurId}.",
                utilisateur.Id);

            StatusMessage = "Votre mot de passe a été changé avec succès.";

            return RedirectToPage();
        }

        private static string MasquerEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return "votre adresse email";
            }

            var arobase = email.IndexOf('@');

            if (arobase <= 0)
            {
                return "votre adresse email";
            }

            return $"{email[0]}***{email[arobase..]}";
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
            _ => erreur.Description
        };
    }
}
