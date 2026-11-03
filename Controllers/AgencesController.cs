// using Microsoft.AspNetCore.Mvc;
// using Microsoft.EntityFrameworkCore;
// using AkarangaBankMvc.Web.Data;
// using AkarangaBankMvc.Web.ViewModels;

// namespace AkarangaBankMvc.Web.Controllers;

// public class AgencesController : Controller
// {
//     private readonly AkarangaBankContext _context;

//     public AgencesController(AkarangaBankContext context)
//     {
//         _context = context;
//     }

//     public async Task<IActionResult> Index()
//     {
//         var agences = await _context.Agences
//             .AsNoTracking()
//             .OrderBy(a => a.Ville)
//             .Select(a => new AgenceListItemViewModel
//             {
//                 Id = a.Id,
//                 Code = a.Code,
//                 Nom = a.Nom,
//                 Ville = a.Ville,
//                 EstSiege = a.EstSiege,
//                 NombreClients = a.Clients.Count()
//             })
//             .ToListAsync();

//         var vm = new AgenceListViewModel
//         {
//             Agences = agences
//         };

//         return View(vm);
//     }

//     public async Task<IActionResult> Details(int id)
//     {
//         var agence = await _context.Agences
//             .AsNoTracking()
//             .FirstOrDefaultAsync(a => a.Id == id);

//         if (agence is null)
//         {
//             return NotFound();
//         }

//         return View(agence);
//     }
// }

// ===========================================================
// Chap 5
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Microsoft.EntityFrameworkCore;
// using AkarangaBankMvc.Web.Services;
using AkarangaBankMvc.Web.ViewModels;
using AkarangaBank.Web.Services;
using Microsoft.AspNetCore.Authorization;

namespace AkarangaBank.Web.Controllers;

public class AgencesController : Controller
{
    private readonly IAgenceService _agenceService;

    public AgencesController(IAgenceService agenceService)
    {
        _agenceService = agenceService;
    }

    // -----------------------------------------------------------------
    // GET: /Agences
    // -----------------------------------------------------------------
    public async Task<IActionResult> Index(string? recherche, int page = 1)
    {
        const int taille = 10;
        var vm = await _agenceService.GetPagedAsync(recherche, page, taille);
        return View(vm);
    }

    // -----------------------------------------------------------------
    // GET: /Agences/Details/5
    // -----------------------------------------------------------------
    public async Task<IActionResult> Details(int id)
    {
        var vm = await _agenceService.GetDetailsAsync(id);
        if (vm is null) return NotFound();
        return View(vm);
    }

    // -----------------------------------------------------------------
    // GET: /Agences/Create
    // -----------------------------------------------------------------
    public IActionResult Create()
    {
        return View(new AgenceCreateViewModel());
    }

    // -----------------------------------------------------------------
    // POST: /Agences/Create
    // -----------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Create(AgenceCreateViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        // Vérification d'unicité du code
        if (await _agenceService.CodeExisteAsync(vm.Code.Trim().ToUpperInvariant()))
        {
            ModelState.AddModelError(nameof(vm.Code),
                "Ce code d'agence est déjà utilisé.");
            return View(vm);
        }

        try
        {
            await _agenceService.CreateAsync(vm);
            TempData["Success"] = "Agence créée avec succès.";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException ex)
        when (ex.InnerException is PostgresException pg
              && pg.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            // Contrainte UNIQUE violée côté base
            ModelState.AddModelError(nameof(vm.Code),
                "Ce code d'agence est déjà utilisé.");
            return View(vm);
        }
    }

    // -----------------------------------------------------------------
    // GET: /Agences/Edit/5
    // -----------------------------------------------------------------
    public async Task<IActionResult> Edit(int id)
    {
        var agence = await _agenceService.GetByIdAsync(id);
        if (agence is null) return NotFound();

        var vm = new AgenceEditViewModel
        {
            Id = agence.Id,
            Code = agence.Code,
            Nom = agence.Nom,
            Ville = agence.Ville,
            EstSiege = agence.EstSiege
        };
        return View(vm);
    }

    // -----------------------------------------------------------------
    // POST: /Agences/Edit/5
    // -----------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Edit(int id, AgenceEditViewModel vm)
    {
        if (id != vm.Id) return BadRequest();
        if (!ModelState.IsValid) return View(vm);

        if (await _agenceService.CodeExisteAsync(vm.Code.Trim().ToUpperInvariant(), vm.Id))
        {
            ModelState.AddModelError(nameof(vm.Code),
                "Ce code d'agence est déjà utilisé.");
            return View(vm);
        }

        try
        {
            var ok = await _agenceService.UpdateAsync(vm);
            if (!ok) return NotFound();

            TempData["Success"] = "Agence modifiée avec succès.";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException pg
              && pg.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            ModelState.AddModelError(nameof(vm.Code),
                "Ce code d'agence est déjà utilisé.");
            return View(vm);
        }
    }

    // -----------------------------------------------------------------
    // GET: /Agences/Delete/5
    // -----------------------------------------------------------------
    public async Task<IActionResult> Delete(int id)
    {
        var agence = await _agenceService.GetByIdAsync(id);
        if (agence is null) return NotFound();
        return View(agence);
    }

    // -----------------------------------------------------------------
    // POST: /Agences/Delete/5
    // -----------------------------------------------------------------
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        try
        {
            var ok = await _agenceService.DeleteAsync(id);
            if (!ok) return NotFound();

            TempData["Success"] = "Agence supprimée avec succès.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}