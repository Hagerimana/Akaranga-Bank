using AkarangaBankMvc.Web.Models;
using AkarangaBankMvc.Web.Services;
using AkarangaBankMvc.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AkarangaBankMvc.Web.Controllers;

[Authorize]
public class DocumentsController : Controller
{
    private readonly IDocumentService _documentService;
    private readonly UserManager<ApplicationUser> _userManager;

    public DocumentsController(
        IDocumentService documentService,
        UserManager<ApplicationUser> userManager)
    {
        _documentService = documentService;
        _userManager = userManager;
    }

    // ============================================================
    // 1. Liste des documents
    // ============================================================
    public async Task<IActionResult> Index(
        string? recherche,
        string? categorie,
        string? statut,
        int? clientId,
        int page = 1)
    {
        var vm = await _documentService.GetPagedAsync(recherche, categorie, statut, clientId, page, 10);
        return View(vm);
    }

    // ============================================================
    // 2. Upload
    // ============================================================
    [HttpGet]
    public IActionResult Upload(int? clientId)
    {
        return View(new DocumentUploadViewModel { ClientId = clientId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(DocumentUploadViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.GetUserAsync(User);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        try
        {
            var doc = await _documentService.UploadAsync(model, user?.Id, ip);
            TempData["Success"] = $"Document '{doc.Titre}' téléversé avec succès (SHA-256 : {doc.Sha256Hash[..8]}...).";
            return RedirectToAction(nameof(Details), new { id = doc.Id });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    // ============================================================
    // 3. Consultation / Détails
    // ============================================================
    public async Task<IActionResult> Details(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var vm = await _documentService.GetDetailsAsync(id, user?.Id, ip);
        if (vm is null)
        {
            return NotFound();
        }

        return View(vm);
    }

    // ============================================================
    // 4. Téléchargement sécurisé
    // ============================================================
    [HttpGet]
    public async Task<IActionResult> Telecharger(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var result = await _documentService.TelechargerAsync(id, user?.Id, ip);
        if (result is null)
        {
            return NotFound();
        }

        var (doc, stream, contentType) = result.Value;
        return File(stream, contentType, doc.NomFichierOriginal);
    }

    // ============================================================
    // 5. Archivage
    // ============================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archiver(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var ok = await _documentService.ArchiverAsync(id, user?.Id, ip);
        if (ok)
        {
            TempData["Success"] = "Le document a été archivé.";
        }
        else
        {
            TempData["Error"] = "Impossible d'archiver ce document.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    // ============================================================
    // 6. Suppression (logique)
    // ============================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Supprimer(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var ok = await _documentService.SupprimerAsync(id, user?.Id, ip);
        if (ok)
        {
            TempData["Success"] = "Le document a été marqué comme supprimé.";
        }
        else
        {
            TempData["Error"] = "Impossible de supprimer ce document.";
        }

        return RedirectToAction(nameof(Index));
    }

    // ============================================================
    // 7. Purge physique (Admin uniquement)
    // ============================================================
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Purger(int jours = 30)
    {
        var purges = await _documentService.PurgerAsync(jours);
        TempData["Success"] = $"{purges} document(s) purgé(s) définitivement du disque.";
        return RedirectToAction(nameof(Index));
    }
}

