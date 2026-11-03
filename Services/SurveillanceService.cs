using AkarangaBankMvc.Web.Data;
using AkarangaBankMvc.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace AkarangaBankMvc.Web.Services;

public class SurveillanceService : ISurveillanceService
{
    private readonly AkarangaBankContext _context;
    private readonly IConformiteService _conformite;

    public SurveillanceService(
        AkarangaBankContext context,
        IConformiteService conformite)
    {
        _context = context;
        _conformite = conformite;
    }

    public async Task<ResultatSurveillance> AnalyserTransactionAsync(
        int compteId,
        decimal montant,
        string typeOperation,
        DateTime date)
    {
        var alertes = new List<AlerteSurveillance>();

        var compte = await _context.Comptes
            .AsNoTracking()
            .Include(c => c.Client)
            .FirstOrDefaultAsync(c => c.Id == compteId);

        if (compte is null)
        {
            return new ResultatSurveillance
            {
                CompteId = compteId,
                DateAnalyse = DateTime.UtcNow,
                Alertes = alertes
            };
        }

        var seuilAML = await _conformite.GetSeuilAMLAsync();

        if (montant >= seuilAML)
        {
            alertes.Add(new AlerteSurveillance
            {
                Regle = "MontantEleve",
                Description =
                    $"Transaction de {montant:N0} BIF, " +
                    $"égale ou supérieure au seuil AML de {seuilAML:N0} BIF.",
                Niveau = NiveauAlerte.Moyen
            });
        }

        var debutSemaine = date.AddDays(-7);

        var nombreTransactions = await _context.Operations
            .AsNoTracking()
            .Where(o => o.CompteId == compteId)
            .Where(o => o.DateOperation >= debutSemaine)
            .Where(o => o.DateOperation <= date)
            .CountAsync();

        if (nombreTransactions >= 20)
        {
            alertes.Add(new AlerteSurveillance
            {
                Regle = "FrequenceElevee",
                Description =
                    $"{nombreTransactions} transactions sur ce compte " +
                    "au cours des 7 derniers jours.",
                Niveau = NiveauAlerte.Eleve
            });
        }

        var risqueClient = compte.ClientId.HasValue
            ? await _conformite.EvaluerRisqueClientAsync(compte.ClientId.Value)
            : NiveauRisqueClient.Inconnu;

        if (risqueClient == NiveauRisqueClient.Eleve)
        {
            alertes.Add(new AlerteSurveillance
            {
                Regle = "ClientARisque",
                Description =
                    "Le client associé à ce compte est classé à risque élevé.",
                Niveau = NiveauAlerte.Eleve
            });
        }

        var moyenne = await GetMoyenneTransactionsAsync(compteId);

        if (moyenne > 0 && montant >= moyenne * 5)
        {
            alertes.Add(new AlerteSurveillance
            {
                Regle = "MontantInhabituel",
                Description =
                    $"Montant de {montant:N0} BIF, au moins 5 fois " +
                    $"la moyenne historique de {moyenne:N0} BIF.",
                Niveau = NiveauAlerte.Moyen
            });
        }

        return new ResultatSurveillance
        {
            CompteId = compteId,
            DateAnalyse = DateTime.UtcNow,
            Alertes = alertes,
            NecessiteDeclaration = alertes.Any(a =>
                a.Niveau == NiveauAlerte.Eleve)
        };
    }

    public async Task<NiveauRisqueClient> EvaluerRisqueClientAsync(
        int clientId)
    {
        return await _conformite.EvaluerRisqueClientAsync(clientId);
    }

    private async Task<decimal> GetMoyenneTransactionsAsync(
        int compteId)
    {
        return await _context.Operations
            .AsNoTracking()
            .Where(o => o.CompteId == compteId)
            .Select(o => (decimal?)o.Montant)
            .AverageAsync() ?? 0m;
    }
}