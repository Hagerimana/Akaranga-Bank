using AkarangaBankMvc.Web.Data;
using AkarangaBankMvc.Web.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AkarangaBankMvc.Web.Services;

public class ReportService : IReportService
{
    private readonly AkarangaBankContext _context;

    public ReportService(AkarangaBankContext context)
    {
        _context = context;
    }

    public async Task<ReportingDashboardViewModel> GetDashboardAsync()
    {
        var premierJourMois = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var totalDepots = await _context.Operations
            .Where(o => o.TypeOperation == "Depot" && o.Statut == "Validee")
            .SumAsync(o => (decimal?)o.Montant) ?? 0m;

        var totalRetraits = await _context.Operations
            .Where(o => o.TypeOperation == "Retrait" && o.Statut == "Validee")
            .SumAsync(o => (decimal?)o.Montant) ?? 0m;

        var totalVirements = await _context.Operations
            .Where(o => (o.TypeOperation == "Virement" || o.TypeOperation == "VirementInterAgence") && o.Statut == "Validee")
            .SumAsync(o => (decimal?)o.Montant) ?? 0m;

        var totalPretsEnCours = await _context.Prets
            .Where(p => p.Statut == "EnCours" || p.Statut == "Impaye")
            .SumAsync(p => (decimal?)p.SoldeRestant) ?? 0m;

        var nbClients = await _context.Clients.CountAsync();
        var nbComptes = await _context.Comptes.CountAsync(c => c.Statut == "Actif");

        var alertesMois = await _context.AlertesAML
            .CountAsync(a => a.DateAlerte >= premierJourMois);

        var declarationsMois = await _context.DeclarationsSoupcon
            .CountAsync(d => d.DateCreation >= premierJourMois);

        var totalDebits = await _context.Ecritures.Where(e => e.Sens == "Debit").SumAsync(e => (decimal?)e.Montant) ?? 0m;
        var totalCredits = await _context.Ecritures.Where(e => e.Sens == "Credit").SumAsync(e => (decimal?)e.Montant) ?? 0m;
        var equilibre = Math.Abs(totalDebits - totalCredits) < 0.01m;

        var volumeTotal = await _context.Operations.Where(o => o.Statut == "Validee").SumAsync(o => (decimal?)o.Montant) ?? 0m;

        return new ReportingDashboardViewModel
        {
            TotalDepots = totalDepots,
            TotalRetraits = totalRetraits,
            TotalVirements = totalVirements,
            TotalCreditsEnCours = totalPretsEnCours,
            NombreClients = nbClients,
            NombreComptesActifs = nbComptes,
            TotalAlertesAmlMois = alertesMois,
            TotalDeclarationsCnrfMois = declarationsMois,
            VolumeTotalTransactions = volumeTotal,
            EstBalanceEquilibree = equilibre
        };
    }

    public async Task<JournalComptableViewModel> GetJournalAsync(
        DateTime? debut,
        DateTime? fin,
        string? typeOp,
        int page,
        int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = pageSize < 1 ? 25 : pageSize;

        var query = _context.Ecritures
            .AsNoTracking()
            .Include(e => e.Operation)
            .Include(e => e.Compte)
                .ThenInclude(c => c!.Client)
            .AsQueryable();

        if (debut.HasValue)
        {
            var d = DateTime.SpecifyKind(debut.Value, DateTimeKind.Utc);
            query = query.Where(e => e.DateEcriture >= d);
        }

        if (fin.HasValue)
        {
            var f = DateTime.SpecifyKind(fin.Value, DateTimeKind.Utc).Date.AddDays(1).AddTicks(-1);
            query = query.Where(e => e.DateEcriture <= f);
        }

        if (!string.IsNullOrWhiteSpace(typeOp))
        {
            query = query.Where(e => e.Operation != null && e.Operation.TypeOperation == typeOp);
        }

        var totalItems = await query.CountAsync();

        var totalDebits = await query.Where(e => e.Sens == "Debit").SumAsync(e => (decimal?)e.Montant) ?? 0m;
        var totalCredits = await query.Where(e => e.Sens == "Credit").SumAsync(e => (decimal?)e.Montant) ?? 0m;

        var rawItems = await query
            .OrderByDescending(e => e.DateEcriture)
            .ThenByDescending(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = rawItems.Select(e => new JournalLigneViewModel
        {
            EcritureId = e.Id,
            DateEcriture = e.DateEcriture,
            ReferenceOperation = e.Operation?.Reference ?? "—",
            TypeOperation = e.Operation?.TypeOperation ?? "—",
            NumeroCompte = e.Compte?.NumeroCompte ?? "—",
            IntituleCompte = e.Compte?.Client != null ? $"{e.Compte.Client.Nom} {e.Compte.Client.Prenom}" : (e.Compte?.EstInterne == true ? "Compte Interne" : e.Compte?.NumeroCompte ?? "—"),
            Libelle = e.Libelle ?? string.Empty,
            Debit = e.Sens == "Debit" ? e.Montant : 0m,
            Credit = e.Sens == "Credit" ? e.Montant : 0m
        }).ToList();

        return new JournalComptableViewModel
        {
            DateDebut = debut,
            DateFin = fin,
            TypeOperation = typeOp,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            Lignes = items,
            TotalDebits = totalDebits,
            TotalCredits = totalCredits
        };
    }

    public async Task<BalanceGeneraleViewModel> GetBalanceGeneraleAsync()
    {
        var comptes = await _context.Comptes
            .AsNoTracking()
            .Include(c => c.TypeCompte)
            .Include(c => c.Client)
            .ToListAsync();

        var ecrituresParCompte = await _context.Ecritures
            .AsNoTracking()
            .GroupBy(e => e.CompteId)
            .Select(g => new
            {
                CompteId = g.Key,
                TotalDebits = g.Where(x => x.Sens == "Debit").Sum(x => x.Montant),
                TotalCredits = g.Where(x => x.Sens == "Credit").Sum(x => x.Montant)
            })
            .ToDictionaryAsync(x => x.CompteId);

        var lignes = new List<BalanceLigneViewModel>();
        foreach (var c in comptes)
        {
            ecrituresParCompte.TryGetValue(c.Id, out var stat);
            var d = stat?.TotalDebits ?? 0m;
            var cr = stat?.TotalCredits ?? 0m;

            var intitule = c.Client != null
                ? $"{c.Client.Nom} {c.Client.Prenom}"
                : (c.EstInterne ? "Compte Interne" : c.NumeroCompte);

            lignes.Add(new BalanceLigneViewModel
            {
                CompteId = c.Id,
                NumeroCompte = c.NumeroCompte,
                Intitule = intitule,
                TypeCompte = c.TypeCompte?.Libelle ?? "Standard",
                TotalDebits = d,
                TotalCredits = cr
            });
        }

        return new BalanceGeneraleViewModel
        {
            DateArrete = DateTime.UtcNow,
            Lignes = lignes.OrderBy(l => l.NumeroCompte).ToList()
        };
    }

    public async Task<CompteResultatViewModel> GetCompteResultatAsync(DateTime debut, DateTime fin)
    {
        var d = DateTime.SpecifyKind(debut, DateTimeKind.Utc);
        var f = DateTime.SpecifyKind(fin, DateTimeKind.Utc).Date.AddDays(1).AddTicks(-1);

        // Produits bancaires : Intérêts perçus sur prêts payés
        var interetsPrets = await _context.EcheancesPret
            .Where(e => e.Statut == "Payee" && e.DatePaiement >= d && e.DatePaiement <= f)
            .SumAsync(e => (decimal?)e.Interet) ?? 0m;

        // Commissions sur opérations de virement et cartes
        var countVirements = await _context.Operations
            .Where(o => o.Statut == "Validee" && (o.TypeOperation == "Virement" || o.TypeOperation == "VirementInterAgence") && o.DateOperation >= d && o.DateOperation <= f)
            .CountAsync();
        var commissionsVirements = countVirements * 1000m; // 1 000 BIF par virement

        var countCartes = await _context.TransactionsCartes
            .Where(t => t.Statut == "Approuvee" && t.DateTransaction >= d && t.DateTransaction <= f)
            .CountAsync();
        var commissionsCartes = countCartes * 500m; // 500 BIF par transaction carte

        var produits = new Dictionary<string, decimal>
        {
            { "Intérêts perçus sur prêts à la clientèle", interetsPrets },
            { "Commissions sur virements bancaires", commissionsVirements },
            { "Commissions d'utilisation cartes bancaires", commissionsCartes }
        };

        // Charges : Intérêts créditeurs sur épargne (1% des soldes d'épargne)
        var interetsEpargne = await _context.Comptes
            .Where(c => c.TypeCompte != null && c.TypeCompte.Code == "EPARGNE" && c.Solde > 0)
            .SumAsync(c => (decimal?)(c.Solde * 0.01m / 12m)) ?? 0m;

        var chargesCompensation = await _context.CompensationsAgences
            .Where(comp => comp.DateCompensation >= d && comp.DateCompensation <= f)
            .SumAsync(comp => (decimal?)(comp.TotalDebits * 0.001m)) ?? 0m;

        var chargesExploitation = 150000m; // Charges informatiques et télécoms forfaitaires

        var charges = new Dictionary<string, decimal>
        {
            { "Intérêts servis sur comptes d'épargne", interetsEpargne },
            { "Frais de compensation interbancaire", chargesCompensation },
            { "Charges générales d'exploitation & IT", chargesExploitation }
        };

        return new CompteResultatViewModel
        {
            DateDebut = debut,
            DateFin = fin,
            Produits = produits,
            Charges = charges
        };
    }

    public async Task<BilanViewModel> GetBilanAsync(DateTime? dateArrete = null)
    {
        var arrete = dateArrete.HasValue
            ? DateTime.SpecifyKind(dateArrete.Value, DateTimeKind.Utc)
            : DateTime.UtcNow;

        // ACTIF
        // 1. Caisse et réserves (Comptes internes de caisse / BRB)
        var totalCaisses = await _context.Comptes
            .Where(c => c.Statut == "Actif" && c.Solde > 0)
            .SumAsync(c => (decimal?)c.Solde) ?? 0m;

        // 2. Prêts et créances sur la clientèle
        var encoursPrets = await _context.Prets
            .Where(p => p.Statut == "EnCours" || p.Statut == "Impaye")
            .SumAsync(p => (decimal?)p.SoldeRestant) ?? 0m;

        // 3. Immobilisations et autres actifs
        var autresActifs = 25000000m; // 25M BIF matériel et équipements

        var actif = new Dictionary<string, decimal>
        {
            { "Disponibilités et caisses agences", totalCaisses },
            { "Créances sur la clientèle (Prêts en cours)", encoursPrets },
            { "Immobilisations et autres actifs", autresActifs }
        };

        // PASSIF
        // 1. Dépôts de la clientèle (soldes des comptes courants et d'épargne)
        var passifDepots = totalCaisses;

        // 2. Dettes interbancaires
        var dettesCompensation = await _context.CompensationsAgences
            .Where(c => c.Statut == "EnCours")
            .SumAsync(c => (decimal?)Math.Abs(c.Solde)) ?? 0m;

        // 3. Fonds propres et capital social
        var capitalSocial = 25000000m;
        var resultat = encoursPrets - dettesCompensation;

        var passif = new Dictionary<string, decimal>
        {
            { "Dépôts et comptes de la clientèle", passifDepots },
            { "Dettes de compensation", dettesCompensation },
            { "Capital social et fonds propres", capitalSocial },
            { "Résultat de l'exercice (Bénéfice reporté)", Math.Max(0, resultat) }
        };

        return new BilanViewModel
        {
            DateArrete = arrete,
            PostesActif = actif,
            PostesPassif = passif
        };
    }

    public async Task<RapportAMLViewModel> GetRapportAMLAsync(int annee, int mois)
    {
        var dateDebut = new DateTime(annee, mois, 1, 0, 0, 0, DateTimeKind.Utc);
        var dateFin = dateDebut.AddMonths(1).AddTicks(-1);

        var alertes = await _context.AlertesAML
            .AsNoTracking()
            .Include(a => a.Client)
            .Where(a => a.DateAlerte >= dateDebut && a.DateAlerte <= dateFin)
            .OrderByDescending(a => a.DateAlerte)
            .ToListAsync();

        var decls = await _context.DeclarationsSoupcon
            .CountAsync(d => d.DateCreation >= dateDebut && d.DateCreation <= dateFin);

        return new RapportAMLViewModel
        {
            Annee = annee,
            Mois = mois,
            TotalAlertes = alertes.Count,
            AlertesCritiques = alertes.Count(a => a.Niveau == "Critique"),
            AlertesElevees = alertes.Count(a => a.Niveau == "Eleve" || a.Niveau == "Élevé"),
            AlertesMoyennes = alertes.Count(a => a.Niveau == "Moyen"),
            AlertesFaibles = alertes.Count(a => a.Niveau == "Faible"),
            AlertesTraitees = alertes.Count(a => a.Statut == "Traitee" || a.Statut == "Classee"),
            DeclarationsCNRF = decls,
            Alertes = alertes
        };
    }
}
