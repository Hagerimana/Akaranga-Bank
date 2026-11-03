using AkarangaBankMvc.Web.Data;
using AkarangaBankMvc.Web.Services;
using AkarangaBankMvc.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AkarangaBankMvc.Web.Controllers;

[Authorize]
public class CartesController : Controller
{
    private readonly ICarteService _carteService;
    private readonly ICurrentUserService _currentUser;
    private readonly AkarangaBankContext _context;

    public CartesController(
        ICarteService carteService,
        ICurrentUserService currentUser,
        AkarangaBankContext context)
    {
        _carteService = carteService;
        _currentUser = currentUser;
        _context = context;
    }

    // GET: /Cartes
    [HttpGet]
    public async Task<IActionResult> Index(
        string? recherche,
        string? statut,
        int page = 1)
    {
        var vm = await _carteService.GetPagedAsync(
            recherche,
            statut,
            page,
            10);

        return View(vm);
    }

    // GET: /Cartes/Details/5
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var vm = await _carteService.GetDetailsAsync(id);

        if (vm is null)
        {
            return NotFound();
        }

        return View(vm);
    }

    // GET: /Cartes/Create
    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Create(int? compteId)
    {
        await ChargerComptesAsync(compteId);

        return View(new CarteCreateViewModel
        {
            CompteId = compteId ?? 0
        });
    }

    // POST: /Cartes/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Create(CarteCreateViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            await ChargerComptesAsync(vm.CompteId);
            return View(vm);
        }

        try
        {
            var carte = await _carteService.EmettreAsync(vm);

            TempData["Success"] =
                $"Carte {carte.TypeCarte} {carte.NumeroMasque} émise. " +
                $"Expiration : {carte.DateExpiration:dd/MM/yyyy}.";

            return RedirectToAction(nameof(Details), new { id = carte.Id });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);

            await ChargerComptesAsync(vm.CompteId);

            return View(vm);
        }
    }

    // POST: /Cartes/Bloquer/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Bloquer(int id, string? motif)
    {
        try
        {
            var carte = await _carteService.BloquerAsync(
                id,
                motif ?? string.Empty);

            TempData["Success"] =
                $"Carte {carte.NumeroMasque} bloquée ({carte.Statut}).";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    // POST: /Cartes/Renouveler/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Renouveler(int id)
    {
        try
        {
            var nouvelle = await _carteService.RenouvelerAsync(id);

            TempData["Success"] =
                $"Carte renouvelée : {nouvelle.NumeroMasque}, " +
                $"expiration {nouvelle.DateExpiration:dd/MM/yyyy}.";

            return RedirectToAction(
                nameof(Details),
                new { id = nouvelle.Id });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    // POST: /Cartes/Transaction
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "GuichetierOnly")]
    public async Task<IActionResult> Transaction(
        CarteTransactionViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] =
                "Les informations de la transaction sont invalides.";

            return RedirectToAction(
                nameof(Details),
                new { id = vm.CarteId });
        }

        try
        {
            var utilisateurId = await _currentUser.GetUserIdAsync();

            var transaction = await _carteService.AutoriserTransactionAsync(
                vm,
                utilisateurId);

            if (transaction.Statut == "Autorisee")
            {
                TempData["Success"] =
                    $"Transaction autorisée : {transaction.Montant:N2} BIF " +
                    $"({transaction.TypeTransaction}).";
            }
            else
            {
                TempData["Error"] =
                    $"Transaction refusée : {transaction.MotifRefus}";
            }
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = vm.CarteId });
    }

    private async Task ChargerComptesAsync(int? compteId = null)
    {
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
                Libelle =
                    $"{c.NumeroCompte} — {c.Client!.Nom} {c.Client.Prenom} — " +
                    $"{c.Solde:N2} {c.Devise}"
            })
            .ToList();

        ViewBag.Comptes = new SelectList(comptes, "Id", "Libelle", compteId);
    }
}
