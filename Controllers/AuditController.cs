using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AkarangaBankMvc.Web.Data;

namespace AkarangaBankMvc.Web.Controllers;

[Authorize(Policy = "AuditeurOnly")]
public class AuditController : Controller
{
    private readonly AkarangaBankContext _context;

    public AuditController(AkarangaBankContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(
        string? action, string? table, DateTime? du, DateTime? au, int page = 1)
    {
        const int taille = 50;
        var query = _context.AuditLogs
            .Include(a => a.Utilisateur)
            .Include(a => a.Agence)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(a => a.Action.Contains(action));

        if (!string.IsNullOrWhiteSpace(table))
            query = query.Where(a => a.TableConcernee == table);

        if (du.HasValue)
            query = query.Where(a => a.DateEvenement >= du.Value);

        if (au.HasValue)
            query = query.Where(a => a.DateEvenement <= au.Value);

        var total = await query.CountAsync();
        var logs = await query
            .OrderByDescending(a => a.DateEvenement)
            .Skip((page - 1) * taille)
            .Take(taille)
            .ToListAsync();

        ViewBag.Action = action;
        ViewBag.Table = table;
        ViewBag.Du = du;
        ViewBag.Au = au;
        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling((double)total / taille);

        return View(logs);
    }

    public async Task<IActionResult> Details(int id)
    {
        var log = await _context.AuditLogs
            .Include(a => a.Utilisateur)
            .Include(a => a.Agence)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (log is null) return NotFound();

        return View(log);
    }
}

