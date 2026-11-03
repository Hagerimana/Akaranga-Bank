using AkarangaBankMvc.Web.Services.Pdf;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AkarangaBankMvc.Web.Controllers;

[Authorize]
public class DocumentsPdfController : Controller
{
    private readonly IPdfService _pdfService;

    public DocumentsPdfController(IPdfService pdfService)
    {
        _pdfService = pdfService;
    }

    [HttpGet]
    public async Task<IActionResult> Releve(int compteId, DateTime? debut, DateTime? fin)
    {
        var d = debut ?? DateTime.UtcNow.AddMonths(-1);
        var f = fin ?? DateTime.UtcNow;

        try
        {
            var pdf = await _pdfService.GenererReleveComptePdfAsync(compteId, d, f);
            var filename = $"Releve_{compteId}_{d:yyyyMMdd}_{f:yyyyMMdd}.pdf";
            return File(pdf, "application/pdf", filename);
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
    }

    [HttpGet]
    public async Task<IActionResult> AttestationSolde(int compteId)
    {
        try
        {
            var pdf = await _pdfService.GenererAttestationSoldePdfAsync(compteId);
            var filename = $"AttestationSolde_{compteId}_{DateTime.UtcNow:yyyyMMdd}.pdf";
            return File(pdf, "application/pdf", filename);
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
    }

    [HttpGet]
    public async Task<IActionResult> ContratPret(int pretId)
    {
        try
        {
            var pdf = await _pdfService.GenererContratPretPdfAsync(pretId);
            var filename = $"ContratPret_{pretId}_{DateTime.UtcNow:yyyyMMdd}.pdf";
            return File(pdf, "application/pdf", filename);
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
    }

    [HttpGet]
    public async Task<IActionResult> DeclarationCnrf(int declarationId)
    {
        try
        {
            var pdf = await _pdfService.GenererDeclarationCnrfPdfAsync(declarationId);
            var filename = $"DeclarationCNRF_{declarationId}_{DateTime.UtcNow:yyyyMMdd}.pdf";
            return File(pdf, "application/pdf", filename);
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
    }

    [HttpGet]
    public async Task<IActionResult> RecuOperation(int operationId)
    {
        try
        {
            var pdf = await _pdfService.GenererRecuOperationPdfAsync(operationId);
            var filename = $"Recu_{operationId}_{DateTime.UtcNow:yyyyMMdd}.pdf";
            return File(pdf, "application/pdf", filename);
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
    }
}

