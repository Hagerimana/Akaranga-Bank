using System.Data;
using AkarangaBankMvc.Web.Data;
using AkarangaBankMvc.Web.Models;
using AkarangaBankMvc.Web.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AkarangaBankMvc.Web.Services;

public class VirementInterAgenceService : IVirementInterAgenceService
{
    private const decimal Maximum = 9_999_999_999_999_999.99m;

    private readonly AkarangaBankContext _context;
    private readonly IConformiteService _conformite;
    private readonly ISurveillanceService _surveillance;
    private readonly IDeclarationService _declaration;
    private readonly AlertesAvanceesService _alertesAvancees;

    public VirementInterAgenceService(
        AkarangaBankContext context,
        IConformiteService conformite,
        ISurveillanceService surveillance,
        IDeclarationService declaration,
        AlertesAvanceesService alertesAvancees)
    {
        _context = context;
        _conformite = conformite;
        _surveillance = surveillance;
        _declaration = declaration;
        _alertesAvancees = alertesAvancees;
    }

    public async Task<VirementInterAgence> InitierAsync(
        VirementInterAgenceViewModel vm,
        int utilisateurId)
    {
        ValiderEntree(
            vm.CompteSourceId,
            vm.CompteDestinationId,
            vm.Montant,
            vm.Motif);

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        await VerifierUtilisateurAsync(utilisateurId);

        var (source, destination) = await ChargerComptesAsync(
            vm.CompteSourceId,
            vm.CompteDestinationId);

        VerifierComptes(source, destination, vm.Montant);

        var analyse = await _conformite.AnalyserTransactionAsync(
            source.Id,
            vm.Montant,
            "Virement");

        if (analyse.EstBloquee)
        {
            throw new InvalidOperationException(
                analyse.Motif ?? "Virement bloqué par la conformité.");
        }

        var date = DateTime.UtcNow;

        var virement = new Virement
        {
            Reference = GenererReference(),
            CompteSourceId = source.Id,
            CompteDestinationId = destination.Id,
            UtilisateurId = utilisateurId,
            Montant = vm.Montant,
            Devise = source.Devise,
            Statut = "EnAttente",
            Motif = vm.Motif?.Trim(),
            ControleAML =
                analyse.NecessiteVigilanceRenforcee ||
                analyse.NecessiteDeclaration,
            DateVirement = date
        };

        var inter = new VirementInterAgence
        {
            Reference = virement.Reference,
            Virement = virement,
            AgenceSourceId = source.AgenceId,
            AgenceDestinationId = destination.AgenceId,
            Montant = vm.Montant,
            Devise = source.Devise,
            Statut = "EnAttente",
            DateCreation = date
        };

        _context.VirementsInterAgences.Add(inter);

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return inter;
    }

    public async Task<bool> ValiderAsync(
        int virementInterAgenceId,
        int valideParId)
    {
        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        await VerifierUtilisateurAsync(valideParId);

        var inter = await _context.VirementsInterAgences
            .Include(v => v.Virement)
            .FirstOrDefaultAsync(v => v.Id == virementInterAgenceId);

        if (inter is null)
        {
            return false;
        }

        VerifierEnAttente(inter);

        var virement = inter.Virement;

        ValiderEntree(
            virement.CompteSourceId,
            virement.CompteDestinationId,
            inter.Montant,
            virement.Motif);

        if (virement.Reference != inter.Reference ||
            virement.Montant != inter.Montant ||
            virement.Devise != inter.Devise)
        {
            throw new InvalidOperationException(
                "Les données du virement et du suivi sont incohérentes.");
        }

        if (await _context.Operations.AnyAsync(o =>
            o.VirementId == virement.Id ||
            o.Reference == inter.Reference))
        {
            throw new InvalidOperationException(
                "Ce virement possède déjà une opération comptable.");
        }

        var (source, destination) = await ChargerComptesAsync(
            virement.CompteSourceId,
            virement.CompteDestinationId);

        VerifierComptes(source, destination, inter.Montant);

        if (source.AgenceId != inter.AgenceSourceId ||
            destination.AgenceId != inter.AgenceDestinationId ||
            source.Devise != inter.Devise)
        {
            throw new InvalidOperationException(
                "L'agence ou la devise d'un compte a changé " +
                "depuis l'initiation.");
        }

        var date = DateTime.UtcNow;
        var jour = date.Date;

        if (await _context.CompensationsAgences.AnyAsync(c =>
            c.DateCompensation == jour &&
            c.Devise == inter.Devise &&
            (c.AgenceId == source.AgenceId ||
             c.AgenceId == destination.AgenceId) &&
            (c.Statut == "Regle" ||
             c.OperationReglementId.HasValue)))
        {
            throw new InvalidOperationException(
                "Une compensation de cette journée est déjà réglée.");
        }

        var analyse = await _conformite.AnalyserTransactionAsync(
            source.Id,
            inter.Montant,
            "Virement");

        if (analyse.EstBloquee)
        {
            throw new InvalidOperationException(
                analyse.Motif ?? "Virement bloqué par la conformité.");
        }

        var fractionnement =
            await _conformite.DetecterFractionnementAsync(
                source.Id,
                inter.Montant,
                date);

        var surveillance =
            await _surveillance.AnalyserTransactionAsync(
                source.Id,
                inter.Montant,
                "Virement",
                date);

        var compensationSource = await ObtenirCompensationAsync(
            source.AgenceId,
            inter.Devise,
            date);

        var compensationDestination = await ObtenirCompensationAsync(
            destination.AgenceId,
            inter.Devise,
            date);

        VerifierCapacite(source.Solde - inter.Montant);
        VerifierCapacite(destination.Solde + inter.Montant);
        VerifierCapacite(compensationSource.Solde + inter.Montant);
        VerifierCapacite(compensationDestination.Solde - inter.Montant);

        var operation = new Operation
        {
            Reference = inter.Reference,
            TypeOperation = "VirementInterAgences",
            CompteId = source.Id,
            AgenceId = source.AgenceId,
            UtilisateurId = valideParId,
            VirementId = virement.Id,
            Montant = inter.Montant,
            Devise = inter.Devise,
            Statut = "Valide",
            DateOperation = date,
            DateValidation = date,
            DateCreation = date,
            Commentaire = virement.Motif
        };

        _context.Operations.Add(operation);
        await _context.SaveChangesAsync();

        var ecritures = new[]
        {
            CreerEcriture(
                operation.Id, source.Id, "Debit", inter.Montant,
                inter.Devise, date,
                $"Virement inter-agences sortant vers {destination.NumeroCompte}"),

            CreerEcriture(
                operation.Id, compensationSource.Id, "Credit",
                inter.Montant, inter.Devise, date,
                $"Compensation agence source {source.AgenceId}"),

            CreerEcriture(
                operation.Id, compensationDestination.Id, "Debit",
                inter.Montant, inter.Devise, date,
                $"Compensation agence destination {destination.AgenceId}"),

            CreerEcriture(
                operation.Id, destination.Id, "Credit", inter.Montant,
                inter.Devise, date,
                $"Virement inter-agences entrant depuis {source.NumeroCompte}")
        };

        var debits = ecritures
            .Where(e => e.Sens == "Debit")
            .Sum(e => e.Montant);

        var credits = ecritures
            .Where(e => e.Sens == "Credit")
            .Sum(e => e.Montant);

        if (debits != credits)
        {
            throw new InvalidOperationException(
                "Les écritures du virement sont déséquilibrées.");
        }

        _context.Ecritures.AddRange(ecritures);

        source.Solde -= inter.Montant;
        destination.Solde += inter.Montant;
        compensationSource.Solde += inter.Montant;
        compensationDestination.Solde -= inter.Montant;

        foreach (var compte in new[]
        {
            source, destination,
            compensationSource, compensationDestination
        })
        {
            compte.DateModification = date;
        }

        virement.Statut = "Valide";
        virement.DateValidation = date;
        virement.ControleAML =
            virement.ControleAML ||
            analyse.NecessiteVigilanceRenforcee ||
            analyse.NecessiteDeclaration ||
            fractionnement ||
            surveillance.Alertes.Any();

        inter.Statut = "EnCompensation";
        inter.ValideParId = valideParId;
        inter.DateCompensation = date;

        await _context.SaveChangesAsync();

        if (analyse.NecessiteDeclaration)
        {
            await _declaration.CreerAlerteAsync(
                source.ClientId!.Value,
                operation.Id,
                "DepassementSeuil",
                analyse.Niveau.ToString(),
                analyse.Motif ?? "Déclaration requise par la conformité.");
        }

        if (fractionnement)
        {
            await _declaration.CreerAlerteAsync(
                source.ClientId!.Value,
                operation.Id,
                "Fractionnement",
                NiveauAlerte.Eleve.ToString(),
                "Suspicion de fractionnement lors du virement inter-agences.");
        }

        foreach (var alerte in surveillance.Alertes)
        {
            await _declaration.CreerAlerteAsync(
                source.ClientId!.Value,
                operation.Id,
                alerte.Regle,
                alerte.Niveau.ToString(),
                alerte.Description);
        }

        await _alertesAvancees.EnregistrerPourOperationAsync(operation.Id);
        await transaction.CommitAsync();

        return true;
    }

    public async Task<bool> RejeterAsync(
        int virementInterAgenceId,
        int valideParId,
        string motif)
    {
        motif = motif?.Trim() ?? string.Empty;

        if (motif.Length == 0 || motif.Length > 500)
        {
            throw new InvalidOperationException(
                "Le motif de rejet doit contenir de 1 à 500 caractères.");
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        await VerifierUtilisateurAsync(valideParId);

        var inter = await _context.VirementsInterAgences
            .Include(v => v.Virement)
            .FirstOrDefaultAsync(v => v.Id == virementInterAgenceId);

        if (inter is null)
        {
            return false;
        }

        VerifierEnAttente(inter);

        if (await _context.Operations.AnyAsync(o =>
            o.VirementId == inter.VirementId))
        {
            throw new InvalidOperationException(
                "Un virement comptabilisé ne peut pas être rejeté.");
        }

        inter.Statut = "Rejete";
        inter.MotifRejet = motif;
        inter.ValideParId = valideParId;
        inter.Virement.Statut = "Rejete";

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return true;
    }

    public async Task<IEnumerable<VirementInterAgence>>
        GetEnAttenteAsync(int agenceId)
    {
        return await RequeteConsultation()
            .Where(v =>
                v.Statut == "EnAttente" &&
                v.AgenceDestinationId == agenceId)
            .OrderByDescending(v => v.DateCreation)
            .ToListAsync();
    }

    public async Task<IEnumerable<VirementInterAgence>>
        GetHistoriqueAsync(
            int agenceId,
            DateTime? du,
            DateTime? au)
    {
        var query = RequeteConsultation()
            .Where(v =>
                v.AgenceSourceId == agenceId ||
                v.AgenceDestinationId == agenceId);

        if (du.HasValue)
        {
            var debut = du.Value.Date;
            query = query.Where(v => v.DateCreation >= debut);
        }

        if (au.HasValue && au.Value.Date < DateTime.MaxValue.Date)
        {
            var fin = au.Value.Date.AddDays(1);
            query = query.Where(v => v.DateCreation < fin);
        }

        return await query
            .OrderByDescending(v => v.DateCreation)
            .ToListAsync();
    }

    private IQueryable<VirementInterAgence> RequeteConsultation()
    {
        return _context.VirementsInterAgences
            .AsNoTracking()
            .Include(v => v.AgenceSource)
            .Include(v => v.AgenceDestination)
            .Include(v => v.ValidePar)
            .Include(v => v.Virement)
                .ThenInclude(v => v.CompteSource)
            .Include(v => v.Virement)
                .ThenInclude(v => v.CompteDestination);
    }

    private async Task<(Compte Source, Compte Destination)>
        ChargerComptesAsync(int sourceId, int destinationId)
    {
        var comptes = await _context.Comptes
            .Where(c => c.Id == sourceId || c.Id == destinationId)
            .OrderBy(c => c.Id)
            .ToListAsync();

        if (comptes.Count != 2)
        {
            throw new InvalidOperationException(
                "Un des comptes est introuvable.");
        }

        foreach (var compte in comptes)
        {
            await _context.Entry(compte).ReloadAsync();
        }

        return (
            comptes.Single(c => c.Id == sourceId),
            comptes.Single(c => c.Id == destinationId));
    }

    private async Task<Compte> ObtenirCompensationAsync(
        int agenceId,
        string devise,
        DateTime date)
    {
        var numero = $"9000-{agenceId:D4}";

        var compte = await _context.Comptes
            .FirstOrDefaultAsync(c => c.NumeroCompte == numero);

        if (compte is not null)
        {
            await _context.Entry(compte).ReloadAsync();

            if (!compte.EstInterne ||
                compte.ClientId.HasValue ||
                compte.AgenceId != agenceId ||
                compte.Devise != devise ||
                compte.Statut != "Actif")
            {
                throw new InvalidOperationException(
                    $"Configuration invalide du compte {numero}.");
            }

            return compte;
        }

        var type = await _context.TypesCompte
            .FirstOrDefaultAsync(t => t.Code == "COURANT")
            ?? throw new InvalidOperationException(
                "Le type de compte COURANT est introuvable.");

        compte = new Compte
        {
            NumeroCompte = numero,
            ClientId = null,
            EstInterne = true,
            TypeCompteId = type.Id,
            AgenceId = agenceId,
            Devise = devise,
            Solde = 0m,
            Statut = "Actif",
            DateOuverture = date,
            DateCreation = date
        };

        _context.Comptes.Add(compte);
        await _context.SaveChangesAsync();

        return compte;
    }

    private async Task VerifierUtilisateurAsync(int id)
    {
        if (id <= 0 || !await _context.Utilisateurs
            .AnyAsync(u => u.Id == id && u.EstActif))
        {
            throw new InvalidOperationException(
                "Utilisateur inexistant ou inactif.");
        }
    }

    private static void VerifierEnAttente(VirementInterAgence inter)
    {
        if (inter.Statut != "EnAttente" ||
            inter.Virement.Statut != "EnAttente")
        {
            throw new InvalidOperationException(
                "Seul un virement en attente peut être validé ou rejeté.");
        }
    }

    private static void VerifierComptes(
        Compte source,
        Compte destination,
        decimal montant)
    {
        if (source.EstInterne || destination.EstInterne ||
            !source.ClientId.HasValue ||
            !destination.ClientId.HasValue)
        {
            throw new InvalidOperationException(
                "Les deux comptes doivent être des comptes clients.");
        }

        if (source.Statut != "Actif" || destination.Statut != "Actif")
        {
            throw new InvalidOperationException(
                "Les deux comptes doivent être actifs.");
        }

        if (source.AgenceId == destination.AgenceId)
        {
            throw new InvalidOperationException(
                "Pour une même agence, utilise le virement standard.");
        }

        if (source.Devise != destination.Devise)
        {
            throw new InvalidOperationException(
                "Les devises des comptes doivent être identiques.");
        }

        if (source.Solde < montant)
        {
            throw new InvalidOperationException(
                "Solde insuffisant sur le compte source.");
        }
    }

    private static void ValiderEntree(
        int sourceId,
        int destinationId,
        decimal montant,
        string? motif)
    {
        if (sourceId <= 0 || destinationId <= 0 ||
            sourceId == destinationId)
        {
            throw new InvalidOperationException(
                "Sélectionne deux comptes différents et valides.");
        }

        if (montant <= 0m || montant > Maximum ||
            decimal.Round(montant, 2) != montant)
        {
            throw new InvalidOperationException(
                "Le montant doit être positif, dans la capacité du champ " +
                "et avoir au maximum deux décimales.");
        }

        if (motif?.Trim().Length > 255)
        {
            throw new InvalidOperationException(
                "Le motif ne peut pas dépasser 255 caractères.");
        }
    }

    private static void VerifierCapacite(decimal solde)
    {
        if (Math.Abs(solde) > Maximum)
        {
            throw new InvalidOperationException(
                "Le solde dépasserait la capacité du champ comptable.");
        }
    }

    private static Ecriture CreerEcriture(
        int operationId,
        int compteId,
        string sens,
        decimal montant,
        string devise,
        DateTime date,
        string libelle)
    {
        return new Ecriture
        {
            OperationId = operationId,
            CompteId = compteId,
            Sens = sens,
            Montant = montant,
            Devise = devise,
            DateEcriture = date,
            Libelle = libelle
        };
    }

    private static string GenererReference()
    {
        return $"VIR-{DateTime.UtcNow:yyyyMMdd}-" +
            Guid.NewGuid().ToString("N")[..16].ToUpperInvariant();
    }
}