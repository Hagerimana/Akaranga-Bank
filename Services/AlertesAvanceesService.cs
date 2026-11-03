using AkarangaBankMvc.Web.Data;
using AkarangaBankMvc.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace AkarangaBankMvc.Web.Services;

public class AlertesAvanceesService
{
    private readonly AkarangaBankContext _context;
    private readonly IDetectionAvanceeService _detection;

    public AlertesAvanceesService(
        AkarangaBankContext context,
        IDetectionAvanceeService detection)
    {
        _context = context;
        _detection = detection;
    }

    public async Task EnregistrerPourOperationAsync(int operationId)
    {
        // Ce service participe à la transaction comptable existante.
        if (_context.Database.IsRelational() && _context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "L'analyse avancée doit être appelée " +
                "dans la transaction de l'opération.");
        }

        var operation = await _context.Operations
            .AsNoTracking()
            .Include(o => o.Compte)
            .FirstOrDefaultAsync(o => o.Id == operationId)
            ?? throw new InvalidOperationException(
                "Opération introuvable pour l'analyse avancée.");

        if (operation.Statut != "Valide" ||
            operation.Compte.EstInterne ||
            !operation.Compte.ClientId.HasValue)
        {
            throw new InvalidOperationException(
                "L'analyse avancée exige une opération validée " +
                "sur un compte client.");
        }

        // Les règles actuelles de DetectionAvanceeService
        // sont limitées au BIF.
        if (operation.Devise != "BIF")
        {
            return;
        }

        var clientId = operation.Compte.ClientId.Value;
        var dateAnalyse = DateTime.UtcNow;

        // Exécution séquentielle : un même DbContext ne doit pas
        // exécuter ces requêtes en parallèle.
        var resultats = new List<ResultatSurveillance>
        {
            await _detection.DetecterStructuringAsync(
                clientId, dateAnalyse),

            await _detection.DetecterSmurfingAsync(
                clientId, dateAnalyse),

            await _detection.DetecterLayeringAsync(
                clientId, dateAnalyse),

            await _detection.DetecterVelociteAnormaleAsync(
                clientId, dateAnalyse)
        };

        var existantes = await _context.AlertesAML
            .Where(a =>
                a.ClientId == clientId &&
                a.OperationId == operationId)
            .Select(a => a.TypeAlerte)
            .ToListAsync();

        var reglesExistantes = existantes.ToHashSet(
            StringComparer.Ordinal);

        foreach (var alerte in resultats.SelectMany(r => r.Alertes))
        {
            // Une seule alerte par règle et par opération.
            if (!reglesExistantes.Add(alerte.Regle))
            {
                continue;
            }

            var description = alerte.Description ?? string.Empty;

            if (description.Length > 500)
            {
                description = description[..500];
            }

            _context.AlertesAML.Add(new AlerteAML
            {
                ClientId = clientId,
                OperationId = operationId,
                TypeAlerte = alerte.Regle,
                Niveau = alerte.Niveau.ToString(),
                Description = description,
                Statut = "Ouverte",
                DateAlerte = dateAnalyse
            });
        }

        await _context.SaveChangesAsync();
    }
}