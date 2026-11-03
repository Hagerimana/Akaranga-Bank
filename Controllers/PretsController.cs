using AkarangaBankMvc.Web.Data;
using AkarangaBankMvc.Web.Models;
using AkarangaBankMvc.Web.Services;
using AkarangaBankMvc.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AkarangaBankMvc.Web.Controllers;

[Authorize]
public class PretsController : Controller
{
    private readonly IPretService _pretService;
    private readonly ICurrentUserService _currentUser;
    private readonly AkarangaBankContext _context;

    public PretsController(
        IPretService pretService,
        ICurrentUserService currentUser,
        AkarangaBankContext context)
    {
        _pretService = pretService;
        _currentUser = currentUser;
        _context = context;
    }

    // GET: /Prets
    [HttpGet]
    public async Task<IActionResult> Index(
        string? recherche,
        string? statut,
        int? typePretId,
        int page = 1)
    {
        var vm = await _pretService.GetPagedAsync(
            recherche,
            statut,
            typePretId,
            page,
            10);

        return View(vm);
    }

    // GET: /Prets/Details/5
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var vm = await _pretService.GetDetailsAsync(id);

        if (vm is null)
        {
            return NotFound();
        }

        return View(vm);
    }

    // GET: /Prets/Create
    [HttpGet]
    [Authorize(Policy = "GuichetierOnly")]
    public async Task<IActionResult> Create()
    {
        await ChargerOptionsAsync();

        var types = ViewBag.TypesPret as IEnumerable<TypePret>
            ?? Enumerable.Empty<TypePret>();

        return View(new PretDemandeViewModel
        {
            TypePretId = types.FirstOrDefault()?.Id ?? 0
        });
    }

    // POST: /Prets/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "GuichetierOnly")]
    public async Task<IActionResult> Create(PretDemandeViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            await ChargerOptionsAsync();
            return View(vm);
        }

        try
        {
            var utilisateurId = await _currentUser.GetUserIdAsync();

            var pret = await _pretService.DemanderAsync(vm, utilisateurId);

            TempData["Success"] =
                $"Demande de prêt {pret.Reference} enregistrée. " +
                $"Mensualité estimée : {pret.Mensualite:N2} BIF.";

            return RedirectToAction(nameof(Details), new { id = pret.Id });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);

            await ChargerOptionsAsync();

            return View(vm);
        }
    }

    // GET: /Prets/Simuler?typePretId=1&montant=500000&dureeMois=12
    [HttpGet]
    [Authorize(Policy = "GuichetierOnly")]
    public async Task<IActionResult> Simuler(
        int typePretId,
        decimal montant,
        int dureeMois)
    {
        var resultat = await _pretService.SimulerAsync(
            typePretId,
            montant,
            dureeMois);

        return Json(resultat);
    }

    // POST: /Prets/Valider/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Valider(int id, decimal tauxInteret)
    {
        try
        {
            var utilisateurId = await _currentUser.GetUserIdAsync();

            var pret = await _pretService.ValiderAsync(
                id,
                tauxInteret,
                utilisateurId);

            TempData["Success"] =
                $"Prêt {pret.Reference} validé au taux de " +
                $"{pret.TauxInteret:N2} %. " +
                $"Mensualité : {pret.Mensualite:N2} BIF.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    // POST: /Prets/Rejeter/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Rejeter(int id, string? motif)
    {
        try
        {
            var utilisateurId = await _currentUser.GetUserIdAsync();

            await _pretService.RejeterAsync(
                id,
                motif ?? string.Empty,
                utilisateurId);

            TempData["Success"] = "Demande de prêt rejetée.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    // POST: /Prets/Debloquer/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Debloquer(int id)
    {
        try
        {
            var utilisateurId = await _currentUser.GetUserIdAsync();

            var pret = await _pretService.DebloquerAsync(id, utilisateurId);

            TempData["Success"] =
                $"Prêt {pret.Reference} débloqué : " +
                $"{pret.MontantPrincipal:N2} BIF crédités sur le compte, " +
                $"{pret.Echeances.Count} échéances générées.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    // POST: /Prets/Rembourser/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "GuichetierOnly")]
    public async Task<IActionResult> Rembourser(int id, int echeanceId)
    {
        try
        {
            var utilisateurId = await _currentUser.GetUserIdAsync();

            var pret = await _pretService.RembourserAsync(
                id,
                echeanceId,
                utilisateurId);

            TempData["Success"] = pret.Statut == "Solde"
                ? $"Prêt {pret.Reference} intégralement remboursé et soldé."
                : $"Échéance remboursée. Solde restant : " +
                  $"{pret.SoldeRestant:N2} BIF.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    // POST: /Prets/MettreAJourRetards
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> MettreAJourRetards()
    {
        try
        {
            var nombre = await _pretService.MettreAJourRetardsAsync();

            TempData["Success"] = nombre == 0
                ? "Aucune nouvelle échéance en retard."
                : $"{nombre} échéance(s) passée(s) en retard.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task ChargerOptionsAsync()
    {
        var clientsEntities = await _context.Clients
            .AsNoTracking()
            .Where(c => c.Statut == "Actif")
            .OrderBy(c => c.Nom)
            .ThenBy(c => c.Prenom)
            .ToListAsync();

        var clients = clientsEntities
            .Select(c => new
            {
                c.Id,
                Libelle =
                    $"{c.NumeroClient} — {c.Nom} {c.Prenom}"
            })
            .ToList();

        ViewBag.Clients = new SelectList(clients, "Id", "Libelle");

        var comptesEntities = await _context.Comptes
            .AsNoTracking()
            .Include(c => c.Client)
            .Where(c =>
                !c.EstInterne &&
                c.ClientId.HasValue &&
                c.Statut == "Actif")
            .OrderBy(c => c.NumeroCompte)
            .ToListAsync();

        var comptes = comptesEntities
            .Select(c => new
            {
                c.Id,
                c.ClientId,
                Libelle =
                    $"{c.NumeroCompte} — {c.Client!.Nom} {c.Client.Prenom} — " +
                    $"{c.Solde:N2} {c.Devise}"
            })
            .ToList();

        ViewBag.Comptes = new SelectList(comptes, "Id", "Libelle");
        ViewBag.ComptesItems = comptes;

        var types = await _pretService.GetTypesActifsAsync();

        ViewBag.TypesPret = types;
    }
}
