#nullable disable

using AkarangaBankMvc.Web.Data;
using AkarangaBankMvc.Web.Models;
using AkarangaBankMvc.Web.Services.Pdf;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AkarangaBankMvc.Web.Areas.Identity.Pages.Account.Manage
{
    public class ProfilModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AkarangaBankContext _context;
        private readonly IProfilePdfService _pdfService;
        private readonly ILogger<ProfilModel> _logger;

        public ProfilModel(
            UserManager<ApplicationUser> userManager,
            AkarangaBankContext context,
            IProfilePdfService pdfService,
            ILogger<ProfilModel> logger)
        {
            _userManager = userManager;
            _context = context;
            _pdfService = pdfService;
            _logger = logger;
        }

        public string NomComplet { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool EmailConfirme { get; set; }
        public string Telephone { get; set; }
        public string RoleMetier { get; set; } = string.Empty;
        public string RolesIdentity { get; set; } = string.Empty;
        public string Agence { get; set; } = string.Empty;
        public DateTime DateCreation { get; set; }
        public DateTime? DerniereConnexion { get; set; }
        public bool DeuxFacteurs { get; set; }
        public string ClientLie { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await ChargerAsync())
            {
                return NotFound("Impossible de charger les informations du compte connecté.");
            }

            return Page();
        }

        public async Task<IActionResult> OnGetTelechargerPdfAsync()
        {
            if (!await ChargerAsync())
            {
                return NotFound("Impossible de charger les informations du compte connecté.");
            }

            var modele = new ProfilePdfModel
            {
                NomComplet = NomComplet,
                Email = Email,
                EmailConfirme = EmailConfirme,
                Telephone = Telephone,
                RoleMetier = RoleMetier,
                RolesIdentity = RolesIdentity,
                Agence = Agence,
                DateCreation = DateCreation,
                DerniereConnexion = DerniereConnexion,
                DeuxFacteurs = DeuxFacteurs,
                ClientLie = ClientLie,
                GenereLe = DateTime.UtcNow
            };

            var pdf = _pdfService.Generer(modele);

            _logger.LogInformation(
                "Fiche PDF des informations personnelles générée pour l'utilisateur {UtilisateurId}.",
                _userManager.GetUserId(User));

            var nomFichier =
                $"informations-personnelles-{DateTime.Now:yyyyMMdd-HHmm}.pdf";

            return File(pdf, "application/pdf", nomFichier);
        }

        private async Task<bool> ChargerAsync()
        {
            var utilisateurId = _userManager.GetUserId(User);

            if (!int.TryParse(utilisateurId, out var id))
            {
                return false;
            }

            var utilisateur = await _context.Utilisateurs
                .AsNoTracking()
                .Include(u => u.Agence)
                .Include(u => u.Client)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (utilisateur is null)
            {
                return false;
            }

            var roles = await _userManager.GetRolesAsync(utilisateur);

            NomComplet = utilisateur.NomComplet;
            Email = utilisateur.Email ?? string.Empty;
            EmailConfirme = utilisateur.EmailConfirmed;
            Telephone = utilisateur.PhoneNumber;
            RoleMetier = utilisateur.Role;
            RolesIdentity = roles.Count == 0 ? "—" : string.Join(", ", roles);
            Agence = utilisateur.Agence is null
                ? "—"
                : $"{utilisateur.Agence.Code} — {utilisateur.Agence.Nom}";
            DateCreation = utilisateur.DateCreation;
            DerniereConnexion = utilisateur.DerniereConnexion;
            DeuxFacteurs = utilisateur.TwoFactorEnabled || utilisateur.DeuxFA_Active;
            ClientLie = utilisateur.Client is null
                ? null
                : $"{utilisateur.Client.NumeroClient} — {utilisateur.Client.Prenom} {utilisateur.Client.Nom}";

            return true;
        }
    }
}
