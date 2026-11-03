using AkarangaBankMvc.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AkarangaBankMvc.Web.Controllers;

[Authorize(Policy = "AdminOnly")]
public class CNRFController : Controller
{
    private readonly ICNRFService _service;
    private readonly ICurrentUserService _currentUser;

    public CNRFController(
        ICNRFService service,
        ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? statut)
    {
        var filtre = string.IsNullOrWhiteSpace(statut)
            ? null
            : statut.Trim();

        ViewBag.Statut = filtre;

        var declarations = await _service.GetDeclarationsAsync(filtre);

        return View(declarations);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        if (id <= 0)
        {
            return NotFound();
        }

        var declarations = await _service.GetDeclarationsAsync(null);

        var declaration = declarations
            .FirstOrDefault(d => d.Id == id);

        if (declaration is null)
        {
            return NotFound();
        }

        return View(declaration);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Generer(int alerteId)
    {
        if (!ModelState.IsValid || alerteId <= 0)
        {
            return BadRequest();
        }

        try
        {
            var utilisateurId = await _currentUser.GetUserIdAsync();

            var declaration = await _service.GenererDOSAsync(
                alerteId,
                utilisateurId);

            TempData["Success"] =
                $"DOS disponible : {declaration.Reference}. " +
                "Document pédagogique, sans envoi à une autorité.";

            return RedirectToAction(
                nameof(Details),
                new { id = declaration.Id });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;

            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Valider(int id)
    {
        if (!ModelState.IsValid || id <= 0)
        {
            return BadRequest();
        }

        try
        {
            var utilisateurId = await _currentUser.GetUserIdAsync();

            var effectue = await _service.ValiderAsync(
                id,
                utilisateurId);

            TempData[effectue ? "Success" : "Error"] = effectue
                ? "Déclaration validée pour le workflow pédagogique."
                : "Aucune modification : déclaration introuvable " +
                  "ou déjà validée.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Transmettre(int id)
    {
        if (!ModelState.IsValid || id <= 0)
        {
            return BadRequest();
        }

        try
        {
            var effectue = await _service.TransmettreAsync(id);

            TempData[effectue ? "Success" : "Error"] = effectue
                ? "Transmission simulée enregistrée. " +
                  "Aucun envoi réel à la CNRF."
                : "Aucune modification : déclaration introuvable " +
                  "ou transmission déjà simulée.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnregistrerAccuse(
        int id,
        string reponse)
    {
        if (!ModelState.IsValid || id <= 0)
        {
            return BadRequest();
        }

        try
        {
            var effectue = await _service.EnregistrerAccuseAsync(
                id,
                reponse);

            TempData[effectue ? "Success" : "Error"] = effectue
                ? "Accusé simulé enregistré. " +
                  "Ce n'est pas une confirmation réelle de la CNRF."
                : "Aucune modification : déclaration introuvable " +
                  "ou accusé déjà enregistré.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }
}