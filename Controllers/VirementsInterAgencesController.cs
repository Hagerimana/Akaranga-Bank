using AkarangaBankMvc.Web.Data;
using AkarangaBankMvc.Web.Models;
using AkarangaBankMvc.Web.Services;
using AkarangaBankMvc.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AkarangaBankMvc.Web.Controllers;

[Authorize(Roles = "admin,guichetier")]
public class VirementsInterAgencesController : Controller
{
    private readonly IVirementInterAgenceService _service;
    private readonly ICurrentUserService _currentUser;
    private readonly AkarangaBankContext _context;

    public VirementsInterAgencesController(
        IVirementInterAgenceService service,
        ICurrentUserService currentUser,
        AkarangaBankContext context)
    {
        _service = service;
        _currentUser = currentUser;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        IEnumerable<VirementInterAgence> virements;

        if (User.IsInRole("admin"))
        {
            virements = await RequeteConsultation()
                .Where(v => v.Statut == "EnAttente")
                .OrderByDescending(v => v.DateCreation)
                .ToListAsync();
        }
        else
        {
            var agenceId = await _currentUser.GetAgenceIdAsync();

            // Le guichetier voit les demandes entrantes
            // et celles initiées depuis son agence.
            virements = await RequeteConsultation()
                .Where(v =>
                    v.Statut == "EnAttente" &&
                    (v.AgenceSourceId == agenceId ||
                     v.AgenceDestinationId == agenceId))
                .OrderByDescending(v => v.DateCreation)
                .ToListAsync();
        }

        return View(virements);
    }

    [HttpGet]
    public async Task<IActionResult> Initier()
    {
        await ChargerComptesAsync();

        return View(new VirementInterAgenceViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Initier(
        VirementInterAgenceViewModel vm)
    {
        if (vm.CompteSourceId > 0 &&
            vm.CompteSourceId == vm.CompteDestinationId)
        {
            ModelState.AddModelError(
                nameof(vm.CompteDestinationId),
                "Les comptes source et destination doivent être différents.");
        }

        if (!ModelState.IsValid)
        {
            await ChargerComptesAsync(
                vm.CompteSourceId,
                vm.CompteDestinationId);

            return View(vm);
        }

        if (!User.IsInRole("admin"))
        {
            var agenceId = await _currentUser.GetAgenceIdAsync();

            var sourceAutorisee = await _context.Comptes
                .AsNoTracking()
                .AnyAsync(c =>
                    c.Id == vm.CompteSourceId &&
                    c.AgenceId == agenceId &&
                    !c.EstInterne &&
                    c.ClientId.HasValue);

            if (!sourceAutorisee)
            {
                return Forbid();
            }
        }

        try
        {
            var utilisateurId = await _currentUser.GetUserIdAsync();

            var virement = await _service.InitierAsync(
                vm,
                utilisateurId);

            TempData["Success"] =
                $"Virement initié : {virement.Reference}. " +
                "En attente de validation ; aucun solde n'a été modifié.";

            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);

            await ChargerComptesAsync(
                vm.CompteSourceId,
                vm.CompteDestinationId);

            return View(vm);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Valider(int id)
    {
        if (!ModelState.IsValid || id <= 0)
        {
            return BadRequest();
        }

        try
        {
            var utilisateurId = await _currentUser.GetUserIdAsync();

            var valide = await _service.ValiderAsync(
                id,
                utilisateurId);

            if (!valide)
            {
                return NotFound();
            }

            TempData["Success"] =
                "Virement validé : les quatre écritures ont été " +
                "enregistrées. Le virement est en compensation.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Rejeter(int id, string motif)
    {
        if (!ModelState.IsValid || id <= 0)
        {
            TempData["Error"] =
                "L'identifiant ou le motif du rejet est invalide.";

            return RedirectToAction(nameof(Index));
        }

        try
        {
            var utilisateurId = await _currentUser.GetUserIdAsync();

            var rejete = await _service.RejeterAsync(
                id,
                utilisateurId,
                motif);

            if (!rejete)
            {
                return NotFound();
            }

            TempData["Success"] =
                "Virement rejeté. Aucun mouvement comptable effectué.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Historique(
        DateTime? du,
        DateTime? au)
    {
        if (!ModelState.IsValid ||
            (du.HasValue && au.HasValue &&
             du.Value.Date > au.Value.Date))
        {
            TempData["Error"] =
                "La période de consultation est invalide.";

            return RedirectToAction(nameof(Index));
        }

        IEnumerable<VirementInterAgence> virements;

        if (User.IsInRole("admin"))
        {
            var query = RequeteConsultation();

            if (du.HasValue)
            {
                var debut = du.Value.Date;
                query = query.Where(v => v.DateCreation >= debut);
            }

            if (au.HasValue &&
                au.Value.Date < DateTime.MaxValue.Date)
            {
                var fin = au.Value.Date.AddDays(1);
                query = query.Where(v => v.DateCreation < fin);
            }

            virements = await query
                .OrderByDescending(v => v.DateCreation)
                .ToListAsync();
        }
        else
        {
            var agenceId = await _currentUser.GetAgenceIdAsync();

            virements = await _service.GetHistoriqueAsync(
                agenceId,
                du,
                au);
        }

        ViewBag.Du = du;
        ViewBag.Au = au;

        return View(virements);
    }

    private IQueryable<VirementInterAgence> RequeteConsultation()
    {
        return _context.VirementsInterAgences
            .AsNoTracking()
            .Include(v => v.AgenceSource)
            .Include(v => v.AgenceDestination)
            .Include(v => v.ValidePar)
            .Include(v => v.Virement)
                .ThenInclude(v => v.CompteSource)
            .Include(v => v.Virement)
                .ThenInclude(v => v.CompteDestination);
    }

    private async Task ChargerComptesAsync(
        int? sourceId = null,
        int? destinationId = null)
    {
        var comptes = await _context.Comptes
            .AsNoTracking()
            .Where(c =>
                c.Statut == "Actif" &&
                !c.EstInterne &&
                c.ClientId.HasValue)
            .OrderBy(c => c.NumeroCompte)
            .Select(c => new
            {
                c.Id,
                c.AgenceId,
                c.NumeroCompte,
                AgenceCode = c.Agence.Code,
                Nom = c.Client!.Nom,
                Prenom = c.Client!.Prenom,
                c.Solde,
                c.Devise
            })
            .ToListAsync();

        var options = comptes
            .Select(c => new
            {
                c.Id,
                c.AgenceId,
                Libelle =
                    $"{c.NumeroCompte} — {c.Nom} {c.Prenom} — " +
                    $"{c.AgenceCode} — {c.Solde:N2} {c.Devise}"
            })
            .ToList();

        var sources = options.AsEnumerable();

        if (!User.IsInRole("admin"))
        {
            var agenceId = await _currentUser.GetAgenceIdAsync();

            sources = sources.Where(c => c.AgenceId == agenceId);
        }

        ViewBag.ComptesSource = new SelectList(
            sources.ToList(),
            "Id",
            "Libelle",
            sourceId);

        ViewBag.ComptesDestination = new SelectList(
            options,
            "Id",
            "Libelle",
            destinationId);
    }
}