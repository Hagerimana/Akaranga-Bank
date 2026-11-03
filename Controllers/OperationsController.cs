using AkarangaBankMvc.Web.Data;
using AkarangaBankMvc.Web.Services;
using AkarangaBankMvc.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AkarangaBankMvc.Web.Controllers;

[Authorize]
public class OperationsController : Controller
{
    private readonly IComptabiliteService _comptabilite;
    private readonly ICurrentUserService _currentUser;
    private readonly AkarangaBankContext _context;

    public OperationsController(
        IComptabiliteService comptabilite,
        ICurrentUserService currentUser,
        AkarangaBankContext context)
    {
        _comptabilite = comptabilite;
        _currentUser = currentUser;
        _context = context;
    }

    [HttpGet]
    [Authorize(Policy = "GuichetierOnly")]
    public async Task<IActionResult> Depot()
    {
        await ChargerComptesAsync();

        return View(new DepotViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "GuichetierOnly")]
    public async Task<IActionResult> Depot(DepotViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            await ChargerComptesAsync(vm.CompteId);
            return View(vm);
        }

        try
        {
            var utilisateurId = await _currentUser.GetUserIdAsync();

            var operation = await _comptabilite.DeposerAsync(
                vm,
                utilisateurId);

            TempData["Success"] =
                $"Dépôt enregistré : référence {operation.Reference}.";

            return RedirectToAction(nameof(Journal));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);

            await ChargerComptesAsync(vm.CompteId);
            return View(vm);
        }
    }

    [HttpGet]
    [Authorize(Policy = "GuichetierOnly")]
    public async Task<IActionResult> Retrait()
    {
        await ChargerComptesAsync();

        return View(new RetraitViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "GuichetierOnly")]
    public async Task<IActionResult> Retrait(RetraitViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            await ChargerComptesAsync(vm.CompteId);
            return View(vm);
        }

        try
        {
            var utilisateurId = await _currentUser.GetUserIdAsync();

            var operation = await _comptabilite.RetirerAsync(
                vm,
                utilisateurId);

            TempData["Success"] =
                $"Retrait enregistré : référence {operation.Reference}.";

            return RedirectToAction(nameof(Journal));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);

            await ChargerComptesAsync(vm.CompteId);
            return View(vm);
        }
    }

    [HttpGet]
    [Authorize(Policy = "GuichetierOnly")]
    public async Task<IActionResult> Virement()
    {
        await ChargerComptesAsync();

        return View(new VirementViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "GuichetierOnly")]
    public async Task<IActionResult> Virement(VirementViewModel vm)
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

        try
        {
            var utilisateurId = await _currentUser.GetUserIdAsync();

            var operation = await _comptabilite.VirerAsync(
                vm,
                utilisateurId);

            TempData["Success"] =
                $"Virement enregistré : référence {operation.Reference}.";

            return RedirectToAction(nameof(Journal));
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

    [HttpGet]
    [Authorize(Roles = "admin,gestionnaire,guichetier,auditeur")]
    public async Task<IActionResult> Journal(
        DateTime? du,
        DateTime? au,
        string? typeOperation,
        int page = 1)
    {
        const int taille = 20;

        page = Math.Max(1, page);

        var vm = await _comptabilite.GetJournalAsync(
            du,
            au,
            typeOperation,
            page,
            taille);

        return View(vm);
    }

    [HttpGet]
    [Authorize(Roles = "admin,gestionnaire,guichetier,auditeur")]
    public async Task<IActionResult> Releve(int id)
    {
        if (id <= 0)
        {
            return NotFound();
        }

        var vm = await _comptabilite.GetReleveAsync(id);

        if (vm is null)
        {
            return NotFound();
        }

        return View(vm);
    }

    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> ControleEquilibre()
    {
        ViewBag.Equilibre =
            await _comptabilite.VerifierEquilibreGlobalAsync();

        return View();
    }

    private async Task ChargerComptesAsync(
        int? sourceId = null,
        int? destinationId = null)
    {
        var comptes = await _context.Comptes
            .AsNoTracking()
            .Include(c => c.Client)
            .Where(c =>
                c.Statut == "Actif" &&
                !c.EstInterne &&
                c.ClientId.HasValue)
            .OrderBy(c => c.NumeroCompte)
            .Select(c => new
            {
                c.Id,
                c.NumeroCompte,
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
                Libelle =
                    $"{c.NumeroCompte} — {c.Nom} {c.Prenom} " +
                    $"— {c.Solde:N2} {c.Devise}"
            })
            .ToList();

        ViewBag.Comptes = new SelectList(
            options,
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