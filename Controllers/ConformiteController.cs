using AkarangaBankMvc.Web.Data;
using AkarangaBankMvc.Web.Models;
using AkarangaBankMvc.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AkarangaBankMvc.Web.Controllers;

[Authorize(Policy = "AdminOnly")]
public class ConformiteController : Controller
{
    private readonly IDeclarationService _declarationService;
    private readonly IConformiteService _conformiteService;
    private readonly ICurrentUserService _currentUser;
    private readonly AkarangaBankContext _context;

    public ConformiteController(
        IDeclarationService declarationService,
        IConformiteService conformiteService,
        ICurrentUserService currentUser,
        AkarangaBankContext context)
    {
        _declarationService = declarationService;
        _conformiteService = conformiteService;
        _currentUser = currentUser;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Alertes(
        string? statut,
        string? niveau)
    {
        statut = string.IsNullOrWhiteSpace(statut)
            ? null
            : statut.Trim();

        niveau = string.IsNullOrWhiteSpace(niveau)
            ? null
            : niveau.Trim();

        var alertes = await _declarationService.GetAlertesAsync(
            statut,
            niveau);

        ViewBag.Statut = statut;
        ViewBag.Niveau = niveau;

        return View(alertes);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        if (id <= 0)
        {
            return NotFound();
        }

        var alerte = await _context.AlertesAML
            .AsNoTracking()
            .Include(a => a.Client)
            .Include(a => a.Operation)
            .Include(a => a.TraitePar)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (alerte is null)
        {
            return NotFound();
        }

        return View(alerte);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Traiter(
    int id,
    string? statut,
    string? commentaire)
    {
        if (id <= 0)
        {
            return NotFound();
        }

        statut = statut?.Trim();

        if (!ModelState.IsValid ||
            statut is not ("EnCours" or "Traitee" or "Classee"))
        {
            return BadRequest(
                "Données de traitement invalides. " +
                "Le statut doit être EnCours, Traitee ou Classee.");
        }

        commentaire = string.IsNullOrWhiteSpace(commentaire)
            ? null
            : commentaire.Trim();

        var utilisateurId = await _currentUser.GetUserIdAsync();

        var ok = await _declarationService.TraiterAlerteAsync(
            id,
            utilisateurId,
            statut,
            commentaire);

        if (!ok)
        {
            return NotFound();
        }

        TempData["Success"] =
            "Le traitement de l'alerte a été enregistré.";

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> RisqueClient(int id)
    {
        if (id <= 0)
        {
            return NotFound();
        }

        var risque = await _conformiteService
            .EvaluerRisqueClientAsync(id);

        if (risque == NiveauRisqueClient.Inconnu)
        {
            return NotFound();
        }

        ViewBag.ClientId = id;
        ViewBag.Risque = risque;

        return View();
    }
}