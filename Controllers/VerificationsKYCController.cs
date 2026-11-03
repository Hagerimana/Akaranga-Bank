// Chap 6
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AkarangaBankMvc.Web.Services;
using AkarangaBankMvc.Web.ViewModels;

namespace AkarangaBankMvc.Web.Controllers;

public class VerificationsKYCController : Controller
{
    private readonly IVerificationKYCService _kycService;
    private readonly IClientService _clientService;
    private readonly ICurrentUserService _currentUserService;

    public VerificationsKYCController(
        IVerificationKYCService kycService,
        IClientService clientService,
        ICurrentUserService currentUserService)
    {
        _kycService = kycService;
        _clientService = clientService;
        _currentUserService = currentUserService;
    }

    // GET: /VerificationsKYC
    // Le point d'entrée KYC affiche les résultats. La création se fait depuis
    // la fiche d'un client afin que le client concerné soit toujours connu.
    public async Task<IActionResult> Index(string? statut)
    {
        ViewBag.Statut = statut;
        return View(await _kycService.GetAllAsync(statut));
    }

    // GET: /VerificationsKYC/Create?clientId=5
    public async Task<IActionResult> Create(int clientId)
    {
        if (clientId <= 0)
        {
            TempData["Error"] = "Sélectionnez d'abord un client pour créer une vérification KYC.";
            return RedirectToAction(nameof(Index));
        }

        var client = await _clientService.GetByIdAsync(clientId);
        if (client is null) return NotFound();

        return View(new VerificationKYCCreateViewModel
        {
            ClientId = client.Id,
            ClientNom = $"{client.Nom} {client.Prenom}",
            ClientNumero = client.NumeroClient
        });
    }

    // POST: /VerificationsKYC/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult>
        Create(VerificationKYCCreateViewModel vm)
    {
        // Never trust the hidden client fields posted by the browser.  Reload
        // the client so an invalid/tampered request cannot become an EF foreign
        // key exception (which previously surfaced as a generic error page).
        var client = await _clientService.GetByIdAsync(vm.ClientId);
        if (client is null)
        {
            return NotFound();
        }

        vm.ClientNom = $"{client.Nom} {client.Prenom}";
        vm.ClientNumero = client.NumeroClient;

        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        // Le vérificateur est l'utilisateur authentifié (plus d'identifiant fictif).
        var verifieParId = await _currentUserService.GetUserIdAsync();

        try
        {
            await _kycService.CreateAsync(vm, verifieParId);
            TempData["Success"] = "Vérification KYC créée. En attente de validation.";
            return RedirectToAction("Details", "Clients", new
            {
                id = vm.ClientId
            });
        }
        catch (DbUpdateException)
        {
            // A database constraint failure is recoverable for the user.  It
            // must not depend on the active UI culture or result in a 500 page.
            ModelState.AddModelError(string.Empty,
                "Impossible d’enregistrer la vérification KYC. Veuillez réessayer.");
            return View(vm);
        }
    }

    // POST: /VerificationsKYC/Valider/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Valider(int id)
    {
        var verifieParId = await _currentUserService.GetUserIdAsync();
        var ok = await _kycService.ValiderAsync(id, verifieParId);
        if (!ok) return NotFound();

        TempData["Success"] = "Vérification KYC validée.";
        return RedirectToAction("Index", "Clients");
    }

    // POST: /VerificationsKYC/Rejeter/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rejeter(int id, string commentaire)
    {
        var verifieParId = await _currentUserService.GetUserIdAsync();
        var ok = await _kycService.RejeterAsync(id, verifieParId,
            commentaire ?? "");
        if (!ok) return NotFound();

        TempData["Success"] = "Vérification KYC rejetée.";
        return RedirectToAction("Index", "Clients");
    }
}
