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
    public class MotDePasseOublieModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICodeVerificationService _codes;
        private readonly ILogger<MotDePasseOublieModel> _logger;

        public MotDePasseOublieModel(
            UserManager<ApplicationUser> userManager,
            ICodeVerificationService codes,
            ILogger<MotDePasseOublieModel> logger)
        {
            _userManager = userManager;
            _codes = codes;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "L'adresse email est obligatoire.")]
            [EmailAddress(ErrorMessage = "Adresse email invalide.")]
            [Display(Name = "Adresse e-mail")]
            public string Email { get; set; }
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var email = Input.Email.Trim();
            var utilisateur = await _userManager.FindByEmailAsync(email);

            // On ne révèle jamais si l'adresse existe ou non.
            TempData["CodeEmail"] = email;

            if (utilisateur is not null && utilisateur.EstActif
                && !string.IsNullOrWhiteSpace(utilisateur.Email))
            {
                try
                {
                    await _codes.EnvoyerCodeAsync(
                        utilisateur,
                        ObjectifCode.ReinitialisationMotDePasse);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Impossible d'envoyer le code de réinitialisation à {Email}.",
                        email);

                    ModelState.AddModelError(
                        string.Empty,
                        "Le code n'a pas pu être envoyé pour le moment. Réessayez dans quelques instants.");
                    return Page();
                }
            }

            return RedirectToPage("./VerifierCode");
        }
    }
}
