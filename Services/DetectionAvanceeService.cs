using AkarangaBankMvc.Web.Data;
using AkarangaBankMvc.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace AkarangaBankMvc.Web.Services;

public class DetectionAvanceeService : IDetectionAvanceeService
{
    private readonly AkarangaBankContext _context;
    private readonly ISeuilAMLService _seuils;

    public DetectionAvanceeService(
        AkarangaBankContext context,
        ISeuilAMLService seuils)
    {
        _context = context;
        _seuils = seuils;
    }

    public async Task<ResultatSurveillance> DetecterStructuringAsync(
        int clientId,
        DateTime date)
    {
        date = await VerifierEntreeAsync(clientId, date);

        var resultat = CreerResultat();
        var seuil = await _seuils.GetSeuilVigilanceAsync();

        var debut = date.AddDays(-30);
        var minimum = seuil * 0.8m;

        var nombre = await OperationsClient(clientId)
            .Where(o =>
                o.DateOperation >= debut &&
                o.DateOperation <= date &&
                o.Montant >= minimum &&
                o.Montant < seuil)
            .CountAsync();

        if (nombre >= 3)
        {
            AjouterAlerte(
                resultat,
                "Structuring",
                $"{nombre} opérations validées entre " +
                $"{minimum:N0} et moins de {seuil:N0} BIF " +
                "sur 30 jours. Fractionnement possible à examiner.",
                NiveauAlerte.Eleve);
        }

        return resultat;
    }

    public async Task<ResultatSurveillance> DetecterSmurfingAsync(
        int clientId,
        DateTime date)
    {
        date = await VerifierEntreeAsync(clientId, date);

        var resultat = CreerResultat();
        var seuil = await _seuils.GetSeuilVigilanceAsync();
        var debut = date.AddDays(-7);

        var parCompte = await OperationsClient(clientId)
            .Where(o =>
                o.DateOperation >= debut &&
                o.DateOperation <= date &&
                o.Montant < seuil)
            .GroupBy(o => o.CompteId)
            .Select(g => new
            {
                CompteId = g.Key,
                Total = g.Sum(o => o.Montant)
            })
            .ToListAsync();

        // Il faut des mouvements sur plusieurs comptes,
        // pas simplement plusieurs comptes ouverts.
        if (parCompte.Count < 2)
        {
            return resultat;
        }

        var total = parCompte.Sum(c => c.Total);

        if (total >= seuil * 3m)
        {
            AjouterAlerte(
                resultat,
                "Smurfing",
                $"Total cumulé de {total:N0} BIF sur " +
                $"{parCompte.Count} comptes utilisés en 7 jours, " +
                "avec des opérations individuellement sous le seuil. " +
                "Répartition inhabituelle à examiner.",
                NiveauAlerte.Critique);
        }

        return resultat;
    }

    public async Task<ResultatSurveillance> DetecterLayeringAsync(
        int clientId,
        DateTime date)
    {
        date = await VerifierEntreeAsync(clientId, date);

        var resultat = CreerResultat();
        var debut = date.AddDays(-14);

        // Utilise le journal pour couvrir aussi les virements
        // inter-agences immédiats enregistrés avant le workflow 9.5.
        var nombre = await OperationsClient(clientId)
            .Where(o =>
                o.TypeOperation == "VirementInterAgences" &&
                o.VirementId.HasValue &&
                o.DateOperation >= debut &&
                o.DateOperation <= date)
            .CountAsync();

        if (nombre >= 5)
        {
            AjouterAlerte(
                resultat,
                "Layering",
                $"{nombre} virements inter-agences sortants validés " +
                "en 14 jours. Indicateur de fréquence à examiner ; " +
                "cette règle ne démontre pas une cascade de fonds.",
                NiveauAlerte.Eleve);
        }

        return resultat;
    }

    public async Task<ResultatSurveillance>
        DetecterVelociteAnormaleAsync(
            int clientId,
            DateTime date)
    {
        date = await VerifierEntreeAsync(clientId, date);

        var resultat = CreerResultat();
        var debut = date.AddHours(-1);

        var nombre = await OperationsClient(clientId)
            .Where(o =>
                o.DateOperation >= debut &&
                o.DateOperation <= date)
            .CountAsync();

        if (nombre >= 10)
        {
            AjouterAlerte(
                resultat,
                "VelociteAnormale",
                $"{nombre} opérations validées en une heure " +
                "sur les comptes du client. " +
                "Fréquence inhabituelle à examiner.",
                NiveauAlerte.Eleve);
        }

        return resultat;
    }

    private IQueryable<Operation> OperationsClient(int clientId)
    {
        return _context.Operations
            .AsNoTracking()
            .Where(o =>
                o.Statut == "Valide" &&
                o.Montant > 0m &&
                o.Devise == "BIF" &&
                !o.Compte.EstInterne &&
                o.Compte.ClientId == clientId &&
                (o.TypeOperation == "Depot" ||
                 o.TypeOperation == "Retrait" ||
                 o.TypeOperation == "Virement" ||
                 o.TypeOperation == "VirementInterAgences"));
    }

    private async Task<DateTime> VerifierEntreeAsync(
        int clientId,
        DateTime date)
    {
        if (clientId <= 0 ||
            !await _context.Clients.AnyAsync(c => c.Id == clientId))
        {
            throw new InvalidOperationException(
                "Client introuvable.");
        }

        // Les dates Unspecified sont interprétées comme UTC.
        var dateUtc = date.Kind switch
        {
            DateTimeKind.Local => date.ToUniversalTime(),
            DateTimeKind.Utc => date,
            _ => DateTime.SpecifyKind(date, DateTimeKind.Utc)
        };

        if (dateUtc < DateTime.MinValue.AddDays(30) ||
            dateUtc > DateTime.UtcNow)
        {
            throw new InvalidOperationException(
                "La date d'analyse doit permettre un historique " +
                "de 30 jours et ne doit pas être dans le futur.");
        }

        return dateUtc;
    }

    private static ResultatSurveillance CreerResultat()
    {
        return new ResultatSurveillance
        {
            // Analyse au niveau client, pas d'un compte unique.
            CompteId = 0,
            DateAnalyse = DateTime.UtcNow,
            Alertes = new List<AlerteSurveillance>(),
            NecessiteDeclaration = false
        };
    }

    private static void AjouterAlerte(
        ResultatSurveillance resultat,
        string regle,
        string description,
        NiveauAlerte niveau)
    {
        resultat.Alertes.Add(new AlerteSurveillance
        {
            Regle = regle,
            Description = description,
            Niveau = niveau
        });

        // Indicateur pédagogique pour la revue conformité.
        // Ne déclenche aucune transmission automatique.
        resultat.NecessiteDeclaration = true;
    }
}