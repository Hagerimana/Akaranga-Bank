using AkarangaBankMvc.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AkarangaBankMvc.Web.Controllers;

[Authorize]
public class NotificationsController : Controller
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public async Task<IActionResult> Index(string? canal, string? statut, int page = 1)
    {
        var vm = await _notificationService.GetPagedAsync(canal, statut, page, 15);
        return View(vm);
    }

    public async Task<IActionResult> Details(int id)
    {
        var vm = await _notificationService.GetDetailsAsync(id);
        if (vm is null)
        {
            return NotFound();
        }

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reessayer()
    {
        var reussis = await _notificationService.ReessayerEchecsAsync();
        TempData["Success"] = $"{reussis} notification(s) renvoyée(s) avec succès.";
        return RedirectToAction(nameof(Index));
    }
}

