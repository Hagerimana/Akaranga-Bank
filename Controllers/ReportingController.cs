using AkarangaBankMvc.Web.Services;
using AkarangaBankMvc.Web.Services.Export;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AkarangaBankMvc.Web.Controllers;

[Authorize]
public class ReportingController : Controller
{
    private readonly IReportService _reportService;
    private readonly IExportService _exportService;

    public ReportingController(
        IReportService reportService,
        IExportService exportService)
    {
        _reportService = reportService;
        _exportService = exportService;
    }

    public async Task<IActionResult> Index()
    {
        var vm = await _reportService.GetDashboardAsync();
        return View(vm);
    }

    public async Task<IActionResult> Journal(
        DateTime? debut,
        DateTime? fin,
        string? typeOp,
        int page = 1)
    {
        var vm = await _reportService.GetJournalAsync(debut, fin, typeOp, page, 30);
        return View(vm);
    }

    public async Task<IActionResult> ExportJournal(
        DateTime? debut,
        DateTime? fin,
        string? typeOp)
    {
        var vm = await _reportService.GetJournalAsync(debut, fin, typeOp, 1, 10000);
        var bytes = _exportService.ExporterJournalCsv(vm);
        var filename = $"JournalComptable_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv";
        return File(bytes, "text/csv; charset=utf-8", filename);
    }

    public async Task<IActionResult> Balance()
    {
        var vm = await _reportService.GetBalanceGeneraleAsync();
        return View(vm);
    }

    public async Task<IActionResult> ExportBalance()
    {
        var vm = await _reportService.GetBalanceGeneraleAsync();
        var bytes = _exportService.ExporterBalanceCsv(vm);
        var filename = $"BalanceGenerale_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv";
        return File(bytes, "text/csv; charset=utf-8", filename);
    }

    public async Task<IActionResult> CompteResultat(DateTime? debut, DateTime? fin)
    {
        var d = debut ?? new DateTime(DateTime.UtcNow.Year, 1, 1);
        var f = fin ?? DateTime.UtcNow;
        var vm = await _reportService.GetCompteResultatAsync(d, f);
        return View(vm);
    }

    public async Task<IActionResult> Bilan(DateTime? dateArrete)
    {
        var vm = await _reportService.GetBilanAsync(dateArrete);
        return View(vm);
    }

    public async Task<IActionResult> RapportAML(int? annee, int? mois)
    {
        var a = annee ?? DateTime.UtcNow.Year;
        var m = mois ?? DateTime.UtcNow.Month;
        var vm = await _reportService.GetRapportAMLAsync(a, m);
        return View(vm);
    }
}

