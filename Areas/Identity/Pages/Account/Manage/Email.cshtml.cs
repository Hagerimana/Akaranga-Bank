#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Text;
using AkarangaBankMvc.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace AkarangaBankMvc.Web.Areas.Identity.Pages.Account.Manage
{
    public class EmailModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailSender<ApplicationUser> _emailSender;
        private readonly ILogger<EmailModel> _logger;

        public EmailModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEmailSender<ApplicationUser> emailSender,
            ILogger<EmailModel> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
            _logger = logger;
        }

        public string Email { get; set; } = string.Empty;

        public bool EmailConfirme { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "La nouvelle adresse email est obligatoire.")]
            [EmailAddress(ErrorMessage = "Adresse email invalide.")]
            [Display(Name = "Nouvelle adresse email")]
            public string NouvelEmail { get; set; }

            [Required(ErrorMessage = "La confirmation est obligatoire.")]
            [EmailAddress(ErrorMessage = "Adresse email invalide.")]
            [Compare(nameof(NouvelEmail), ErrorMessage = "Les deux adresses ne correspondent pas.")]
            [Display(Name = "Confirmer la nouvelle adresse")]
            public string ConfirmationEmail { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var utilisateur = await _userManager.GetUserAsync(User);

            if (utilisateur is null)
            {
                return NotFound($"Impossible de charger l'utilisateur '{_userManager.GetUserId(User)}'.");
            }

            await ChargerAsync(utilisateur);
            return Page();
        }

        public async Task<IActionResult> OnPostChangerEmailAsync()
        {
            var utilisateur = await _userManager.GetUserAsync(User);

            if (utilisateur is null)
            {
                return NotFound($"Impossible de charger l'utilisateur '{_userManager.GetUserId(User)}'.");
            }

            await ChargerAsync(utilisateur);

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var nouvelEmail = Input.NouvelEmail.Trim();

            if (string.Equals(nouvelEmail, utilisateur.Email, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(
                    "Input.NouvelEmail",
                    "Cette adresse est déjà celle de votre compte.");
                return Page();
            }

            var existant = await _userManager.FindByEmailAsync(nouvelEmail);

            if (existant is not null && existant.Id != utilisateur.Id)
            {
                ModelState.AddModelError(
                    "Input.NouvelEmail",
                    "Cette adresse email est déjà utilisée par un autre compte.");
                return Page();
            }

            var changement = await _userManager.SetEmailAsync(utilisateur, nouvelEmail);

            if (!changement.Succeeded)
            {
                foreach (var erreur in changement.Errors)
                {
                    ModelState.AddModelError(string.Empty, erreur.Description);
                }

                return Page();
            }

            try
            {
                await EnvoyerLienConfirmationAsync(utilisateur, nouvelEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Impossible d'envoyer le lien de confirmation à {Email}.",
                    nouvelEmail);

                StatusMessage =
                    "L'adresse a été modifiée, mais le lien de confirmation n'a pas pu être envoyé. " +
                    "Réessayez depuis cette page après vous être reconnecté.";

                return RedirectToPage();
            }

            await _signInManager.SignOutAsync();

            TempData["SuccessMessage"] =
                $"Un lien de confirmation a été envoyé à {nouvelEmail}. " +
                "Consultez votre boîte de réception puis connectez-vous depuis ce lien.";

            return RedirectToPage("/Account/Login", new { area = "Identity" });
        }

        public async Task<IActionResult> OnPostRenvoyerVerificationAsync()
        {
            var utilisateur = await _userManager.GetUserAsync(User);

            if (utilisateur is null)
            {
                return NotFound($"Impossible de charger l'utilisateur '{_userManager.GetUserId(User)}'.");
            }

            await ChargerAsync(utilisateur);

            if (string.IsNullOrWhiteSpace(utilisateur.Email))
            {
                StatusMessage = "Aucune adresse email n'est associée à ce compte.";
                return RedirectToPage();
            }

            try
            {
                await EnvoyerLienConfirmationAsync(utilisateur, utilisateur.Email);
                StatusMessage = "Un nouveau lien de vérification vient d'être envoyé.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Échec du renvoi du lien de confirmation.");

                StatusMessage = "Le lien de confirmation n'a pas pu être envoyé. Réessayez plus tard.";
            }

            return RedirectToPage();
        }

        private async Task ChargerAsync(ApplicationUser utilisateur)
        {
            Email = utilisateur.Email ?? string.Empty;
            EmailConfirme = utilisateur.EmailConfirmed;
            Input ??= new InputModel();
        }

        private async Task EnvoyerLienConfirmationAsync(
            ApplicationUser utilisateur,
            string destinataire)
        {
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(utilisateur);
            var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

            var lien = Url.Page(
                "/Account/ConfirmerEmail",
                pageHandler: null,
                values: new
                {
                    area = "Identity",
                    userId = utilisateur.Id,
                    code
                },
                protocol: Request.Scheme)!;

            await _emailSender.SendConfirmationLinkAsync(utilisateur, destinataire, lien);

            _logger.LogInformation(
                "Lien de confirmation d'email envoyé à {Email} pour l'utilisateur {UtilisateurId}.",
                destinataire,
                utilisateur.Id);
        }
    }
}
