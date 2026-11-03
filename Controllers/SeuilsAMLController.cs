using AkarangaBankMvc.Web.Data;
using AkarangaBankMvc.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AkarangaBankMvc.Web.Controllers;

[Authorize(Policy = "AdminOnly")]
public class SeuilsAMLController : Controller
{
    private readonly ISeuilAMLService _service;
    private readonly ICurrentUserService _currentUser;
    private readonly AkarangaBankContext _context;

    public SeuilsAMLController(
        ISeuilAMLService service,
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
        var seuils = await _service.GetAllAsync();

        var audits = await _context.AuditLogs
            .AsNoTracking()
            .Include(a => a.Utilisateur)
            .Where(a =>
                a.TableConcernee == "SeuilsAML" &&
                a.Action == "ModificationSeuilAML")
            .OrderByDescending(a => a.DateEvenement)
            .ThenByDescending(a => a.Id)
            .Take(20)
            .ToListAsync();

        ViewBag.Audits = audits;

        return View(seuils);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Modifier(
        int id,
        decimal nouveauMontant)
    {
        if (!ModelState.IsValid || id <= 0)
        {
            TempData["Error"] =
                "Identifiant ou montant invalide. " +
                "Saisis un montant positif sans séparateur de milliers.";

            return RedirectToAction(nameof(Index));
        }

        try
        {
            var utilisateurId = await _currentUser.GetUserIdAsync();

            var modifie = await _service.MettreAJourAsync(
                id,
                nouveauMontant,
                utilisateurId);

            if (modifie)
            {
                TempData["Success"] =
                    "Seuil modifié. Une trace d'audit a été enregistrée.";
            }
            else
            {
                TempData["Error"] =
                    "Aucune modification : montant identique " +
                    "ou seuil introuvable.";
            }
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}