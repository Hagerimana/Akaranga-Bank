using AkarangaBankMvc.Web.Data;
using AkarangaBankMvc.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AkarangaBankMvc.Web.Controllers;

[Authorize(Policy = "AdminOnly")]
public class CompensationsController : Controller
{
    private readonly ICompensationService _service;
    private readonly AkarangaBankContext _context;

    public CompensationsController(
        ICompensationService service,
        AkarangaBankContext context)
    {
        _service = service;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        int? agenceId,
        DateTime? date,
        string? devise)
    {
        var jour = date?.Date ?? DateTime.UtcNow.Date;

        var monnaie = string.IsNullOrWhiteSpace(devise)
            ? "BIF"
            : devise.Trim().ToUpperInvariant();

        if (monnaie.Length != 3 ||
            monnaie.Any(c => c < 'A' || c > 'Z'))
        {
            TempData["Error"] =
                "La devise doit contenir trois lettres.";
            monnaie = "BIF";
        }

        var agences = await _context.Agences
            .AsNoTracking()
            .OrderBy(a => a.Code)
            .Select(a => new
            {
                a.Id,
                Libelle = a.Code + " - " + a.Ville
            })
            .ToListAsync();

        ViewBag.Agences =
            new SelectList(agences, "Id", "Libelle", agenceId);

        ViewBag.AgenceId = agenceId;
        ViewBag.Date = jour;
        ViewBag.Devise = monnaie;

        var compensations = await _service.GetCompensationsAsync(
            agenceId,
            jour,
            monnaie);

        return View(compensations);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Calculer(
        int agenceId,
        DateTime date,
        string devise = "BIF")
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] =
                "Vérifie l'agence, la date et la devise.";

            return RedirectToAction(nameof(Index));
        }

        try
        {
            var compensation =
                await _service.CalculerCompensationAsync(
                    agenceId,
                    date,
                    devise);

            TempData["Success"] =
                $"Compensation calculée : {compensation.Reference}.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new
        {
            agenceId,
            date = date.ToString("yyyy-MM-dd"),
            devise
        });
    }

    [HttpGet]
    public async Task<IActionResult> Verifier(
    DateTime date,
    string devise = "BIF")
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] =
                "La date ou la devise est invalide.";

            return RedirectToAction(nameof(Index));
        }

        try
        {
            var equilibre =
                await _service.VerifierEquilibreCompensationsAsync(
                    date,
                    devise);

            if (equilibre)
            {
                TempData["Success"] =
                    "Compensations équilibrées : les fiches correspondent " +
                    "aux écritures et le total des débits égale " +
                    "le total des crédits.";
            }
            else
            {
                TempData["Error"] =
                    "Contrôle non validé : aucune écriture de compensation, " +
                    "fiche manquante, calcul à actualiser ou déséquilibre. " +
                    "Calcule ou recalcule les agences concernées.";
            }
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new
        {
            date = date.ToString("yyyy-MM-dd"),
            devise
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Regler(
    int compensationPositiveId,
    int compensationNegativeId,
    DateTime date,
    string devise,
    [FromServices] ICurrentUserService currentUser)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Les données du règlement sont invalides.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var utilisateurId = await currentUser.GetUserIdAsync();

            var effectue = await _service.ReglerCompensationsAsync(
                compensationPositiveId,
                compensationNegativeId,
                utilisateurId);

            TempData["Success"] = effectue
                ? "Les deux compensations ont été réglées."
                : "Ces compensations étaient déjà réglées : aucune nouvelle écriture.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new
        {
            date = date.ToString("yyyy-MM-dd"),
            devise
        });
    }

    
}