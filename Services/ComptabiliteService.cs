using System.Data;
using AkarangaBankMvc.Web.Data;
using AkarangaBankMvc.Web.Models;
using AkarangaBankMvc.Web.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AkarangaBankMvc.Web.Services;

public class ComptabiliteService : IComptabiliteService
{
    private readonly AkarangaBankContext _context;
    private readonly IConformiteService _conformite;
    private readonly ISurveillanceService _surveillance;
    private readonly IDeclarationService _declaration;

    private readonly AlertesAvanceesService _alertesAvancees;

    public ComptabiliteService(
        AkarangaBankContext context,
        IConformiteService conformite,
        ISurveillanceService surveillance,
        IDeclarationService declaration,
        AlertesAvanceesService alertesAvancees
        )
    {
        _context = context;
        _conformite = conformite;
        _surveillance = surveillance;
        _declaration = declaration;
        _alertesAvancees = alertesAvancees;
    }

    public Task<Operation> DeposerAsync(
        DepotViewModel vm,
        int utilisateurId)
    {
        return ExecuterOperationEspecesAsync(
            vm.CompteId,
            vm.Montant,
            utilisateurId,
            vm.Commentaire,
            "Depot");
    }

    public Task<Operation> RetirerAsync(
        RetraitViewModel vm,
        int utilisateurId)
    {
        return ExecuterOperationEspecesAsync(
            vm.CompteId,
            vm.Montant,
            utilisateurId,
            vm.Commentaire,
            "Retrait");
    }

    private async Task<Operation> ExecuterOperationEspecesAsync(
        int compteId,
        decimal montant,
        int utilisateurId,
        string? commentaire,
        string typeOperation)
    {
        ValiderEntree(
            compteId,
            montant,
            utilisateurId,
            commentaire);

        if (typeOperation is not ("Depot" or "Retrait"))
        {
            throw new InvalidOperationException(
                "Le type d'opération en espèces est invalide.");
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        try
        {
            var compte = await ChargerCompteAsync(compteId);

            VerifierCompteActif(compte);

            if (compte.EstInterne || !compte.ClientId.HasValue)
            {
                throw new InvalidOperationException(
                    "Les dépôts et retraits doivent être effectués sur un compte client.");
            }

            if (typeOperation == "Retrait" && compte.Solde < montant)
            {
                throw new InvalidOperationException(
                    "Solde insuffisant.");
            }

            var date = DateTime.UtcNow;

            var analyse = await _conformite.AnalyserTransactionAsync(
                compte.Id,
                montant,
                typeOperation);

            if (analyse.EstBloquee)
            {
                throw new InvalidOperationException(
                    analyse.Motif ?? "Opération bloquée.");
            }

            var fractionnement =
                await _conformite.DetecterFractionnementAsync(
                    compte.Id,
                    montant,
                    date);

            var surveillance =
                await _surveillance.AnalyserTransactionAsync(
                    compte.Id,
                    montant,
                    typeOperation,
                    date);

            var operation = new Operation
            {
                Reference = GenererReference("AKR"),
                TypeOperation = typeOperation,
                CompteId = compte.Id,
                UtilisateurId = utilisateurId,
                AgenceId = compte.AgenceId,
                Montant = montant,
                Devise = compte.Devise,
                Statut = "Valide",
                DateOperation = date,
                DateValidation = date,
                DateCreation = date,
                Commentaire = commentaire?.Trim()
            };

            _context.Operations.Add(operation);
            await _context.SaveChangesAsync();

            var estDepot = typeOperation == "Depot";

            var compteCaisse = await ObtenirCompteCaisseAsync(
                compte.AgenceId,
                estDepot ? "DEPOT" : "RETRAIT",
                compte.Devise,
                date);

            Ecriture debit;
            Ecriture credit;

            if (estDepot)
            {
                debit = new Ecriture
                {
                    OperationId = operation.Id,
                    CompteId = compteCaisse.Id,
                    Sens = "Debit",
                    Montant = montant,
                    Devise = compte.Devise,
                    DateEcriture = date,
                    Libelle =
                        $"Encaissement dépôt pour {compte.NumeroCompte}"
                };

                credit = new Ecriture
                {
                    OperationId = operation.Id,
                    CompteId = compte.Id,
                    Sens = "Credit",
                    Montant = montant,
                    Devise = compte.Devise,
                    DateEcriture = date,
                    Libelle =
                        $"Dépôt sur compte {compte.NumeroCompte}"
                };

                compte.Solde += montant;
                compteCaisse.Solde += montant;
            }
            else
            {
                debit = new Ecriture
                {
                    OperationId = operation.Id,
                    CompteId = compte.Id,
                    Sens = "Debit",
                    Montant = montant,
                    Devise = compte.Devise,
                    DateEcriture = date,
                    Libelle =
                        $"Retrait du compte {compte.NumeroCompte}"
                };

                credit = new Ecriture
                {
                    OperationId = operation.Id,
                    CompteId = compteCaisse.Id,
                    Sens = "Credit",
                    Montant = montant,
                    Devise = compte.Devise,
                    DateEcriture = date,
                    Libelle =
                        $"Décaissement retrait pour {compte.NumeroCompte}"
                };

                compte.Solde -= montant;
                compteCaisse.Solde -= montant;
            }

            VerifierEquilibre(new[] { debit, credit });

            _context.Ecritures.AddRange(debit, credit);

            compte.DateModification = date;
            compteCaisse.DateModification = date;

            await _context.SaveChangesAsync();

            await CreerAlertesSiNecessaireAsync(
                compte.ClientId.Value,
                operation.Id,
                analyse,
                fractionnement,
                surveillance);

            await _alertesAvancees.EnregistrerPourOperationAsync(operation.Id);
            await transaction.CommitAsync();

            return operation;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<Operation> VirerAsync(
    VirementViewModel vm,
    int utilisateurId)
    {
        ValiderEntree(
            vm.CompteSourceId,
            vm.Montant,
            utilisateurId,
            vm.Motif);

        if (vm.CompteDestinationId <= 0)
        {
            throw new InvalidOperationException(
                "Un compte destination valide doit être sélectionné.");
        }

        if (vm.CompteSourceId == vm.CompteDestinationId)
        {
            throw new InvalidOperationException(
                "Les comptes source et destination doivent être différents.");
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        try
        {
            var premierId = Math.Min(
                vm.CompteSourceId,
                vm.CompteDestinationId);

            var secondId = Math.Max(
                vm.CompteSourceId,
                vm.CompteDestinationId);

            var premier = await ChargerCompteAsync(premierId);
            var second = await ChargerCompteAsync(secondId);

            var source = premier.Id == vm.CompteSourceId
                ? premier
                : second;

            var destination = premier.Id == vm.CompteDestinationId
                ? premier
                : second;

            VerifierCompteActif(source);
            VerifierCompteActif(destination);

            if (source.EstInterne || destination.EstInterne ||
                !source.ClientId.HasValue ||
                !destination.ClientId.HasValue)
            {
                throw new InvalidOperationException(
                    "Les virements standards sont réservés aux comptes clients.");
            }

            if (source.Devise != destination.Devise)
            {
                throw new InvalidOperationException(
                    "Les devises des deux comptes doivent être identiques.");
            }

            if (source.Solde < vm.Montant)
            {
                throw new InvalidOperationException(
                    "Solde insuffisant sur le compte source.");
            }

            var date = DateTime.UtcNow;
            var estInterAgences =
                source.AgenceId != destination.AgenceId;

            var analyse = await _conformite.AnalyserTransactionAsync(
                source.Id,
                vm.Montant,
                "Virement");

            if (analyse.EstBloquee)
            {
                throw new InvalidOperationException(
                    analyse.Motif ?? "Opération bloquée.");
            }

            var fractionnement =
                await _conformite.DetecterFractionnementAsync(
                    source.Id,
                    vm.Montant,
                    date);

            var surveillance =
                await _surveillance.AnalyserTransactionAsync(
                    source.Id,
                    vm.Montant,
                    "Virement",
                    date);

            var virement = new Virement
            {
                Reference = GenererReference("VIR"),
                CompteSourceId = source.Id,
                CompteDestinationId = destination.Id,
                UtilisateurId = utilisateurId,
                Montant = vm.Montant,
                Devise = source.Devise,
                Statut = "Valide",
                Motif = vm.Motif?.Trim(),
                ControleAML =
                    analyse.NecessiteVigilanceRenforcee ||
                    analyse.NecessiteDeclaration ||
                    fractionnement ||
                    surveillance.Alertes.Any(),
                DateVirement = date,
                DateValidation = date
            };

            _context.Virements.Add(virement);
            await _context.SaveChangesAsync();

            var operation = new Operation
            {
                Reference = virement.Reference,
                TypeOperation = estInterAgences
                    ? "VirementInterAgences"
                    : "Virement",
                CompteId = source.Id,
                UtilisateurId = utilisateurId,
                AgenceId = source.AgenceId,
                VirementId = virement.Id,
                Montant = vm.Montant,
                Devise = source.Devise,
                Statut = "Valide",
                DateOperation = date,
                DateValidation = date,
                DateCreation = date,
                Commentaire = vm.Motif?.Trim()
            };

            _context.Operations.Add(operation);
            await _context.SaveChangesAsync();

            var ecritures = new List<Ecriture>();

            if (estInterAgences)
            {
                var compensationSource =
                    await ObtenirCompteCompensationAsync(
                        source.AgenceId,
                        source.Devise,
                        date);

                var compensationDestination =
                    await ObtenirCompteCompensationAsync(
                        destination.AgenceId,
                        destination.Devise,
                        date);

                ecritures.Add(new Ecriture
                {
                    OperationId = operation.Id,
                    CompteId = source.Id,
                    Sens = "Debit",
                    Montant = vm.Montant,
                    Devise = source.Devise,
                    DateEcriture = date,
                    Libelle =
                        $"Virement inter-agences sortant vers {destination.NumeroCompte}"
                });

                ecritures.Add(new Ecriture
                {
                    OperationId = operation.Id,
                    CompteId = compensationSource.Id,
                    Sens = "Credit",
                    Montant = vm.Montant,
                    Devise = source.Devise,
                    DateEcriture = date,
                    Libelle =
                        $"Compensation agence source {source.AgenceId}"
                });

                ecritures.Add(new Ecriture
                {
                    OperationId = operation.Id,
                    CompteId = compensationDestination.Id,
                    Sens = "Debit",
                    Montant = vm.Montant,
                    Devise = destination.Devise,
                    DateEcriture = date,
                    Libelle =
                        $"Compensation agence destination {destination.AgenceId}"
                });

                ecritures.Add(new Ecriture
                {
                    OperationId = operation.Id,
                    CompteId = destination.Id,
                    Sens = "Credit",
                    Montant = vm.Montant,
                    Devise = destination.Devise,
                    DateEcriture = date,
                    Libelle =
                        $"Virement inter-agences entrant depuis {source.NumeroCompte}"
                });

                compensationSource.Solde += vm.Montant;
                compensationDestination.Solde -= vm.Montant;

                compensationSource.DateModification = date;
                compensationDestination.DateModification = date;
            }
            else
            {
                ecritures.Add(new Ecriture
                {
                    OperationId = operation.Id,
                    CompteId = source.Id,
                    Sens = "Debit",
                    Montant = vm.Montant,
                    Devise = source.Devise,
                    DateEcriture = date,
                    Libelle =
                        $"Virement sortant vers {destination.NumeroCompte}"
                });

                ecritures.Add(new Ecriture
                {
                    OperationId = operation.Id,
                    CompteId = destination.Id,
                    Sens = "Credit",
                    Montant = vm.Montant,
                    Devise = destination.Devise,
                    DateEcriture = date,
                    Libelle =
                        $"Virement entrant depuis {source.NumeroCompte}"
                });
            }

            VerifierEquilibre(ecritures);

            _context.Ecritures.AddRange(ecritures);

            source.Solde -= vm.Montant;
            destination.Solde += vm.Montant;

            source.DateModification = date;
            destination.DateModification = date;

            await _context.SaveChangesAsync();

            await CreerAlertesSiNecessaireAsync(
                source.ClientId.Value,
                operation.Id,
                analyse,
                fractionnement,
                surveillance);
            
            await _alertesAvancees.EnregistrerPourOperationAsync(operation.Id);
            await transaction.CommitAsync();

            return operation;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<JournalViewModel> GetJournalAsync(
        DateTime? du,
        DateTime? au,
        string? typeOperation,
        int page,
        int taille)
    {
        page = Math.Max(1, page);
        taille = taille < 1 ? 20 : Math.Min(taille, 50);

        var query = _context.Operations
            .AsNoTracking()
            .Include(o => o.Compte)
            .Include(o => o.Utilisateur)
            .Include(o => o.Ecritures)
                .ThenInclude(e => e.Compte)
            .AsQueryable();

        if (du.HasValue)
        {
            var debut = du.Value.Date;
            query = query.Where(o => o.DateOperation >= debut);
        }

        if (au.HasValue && au.Value.Date < DateTime.MaxValue.Date)
        {
            var fin = au.Value.Date.AddDays(1);
            query = query.Where(o => o.DateOperation < fin);
        }

        typeOperation = string.IsNullOrWhiteSpace(typeOperation)
            ? null
            : typeOperation.Trim();

        if (typeOperation is not null)
        {
            query = query.Where(
                o => o.TypeOperation == typeOperation);
        }

        var total = await query.CountAsync();

        var totalPages = total == 0
            ? 1
            : (int)Math.Ceiling((double)total / taille);

        page = Math.Min(page, totalPages);

        var operations = await query
            .OrderByDescending(o => o.DateOperation)
            .ThenByDescending(o => o.Id)
            .Skip((page - 1) * taille)
            .Take(taille)
            .ToListAsync();

        return new JournalViewModel
        {
            Operations = operations,
            Du = du,
            Au = au,
            TypeOperation = typeOperation,
            Page = page,
            TaillePage = taille,
            Total = total
        };
    }

    public async Task<ReleveViewModel?> GetReleveAsync(int compteId)
    {
        var compte = await _context.Comptes
            .AsNoTracking()
            .Include(c => c.Client)
            .FirstOrDefaultAsync(c => c.Id == compteId);

        if (compte is null)
        {
            return null;
        }

        var ecritures = await _context.Ecritures
            .AsNoTracking()
            .Include(e => e.Operation)
            .Where(e => e.CompteId == compteId)
            .OrderBy(e => e.DateEcriture)
            .ThenBy(e => e.Id)
            .ToListAsync();

        var totalCredits = ecritures
            .Where(e => e.Sens == "Credit")
            .Sum(e => e.Montant);

        var totalDebits = ecritures
            .Where(e => e.Sens == "Debit")
            .Sum(e => e.Montant);

        return new ReleveViewModel
        {
            Compte = compte,
            SoldeInitial = compte.Solde - totalCredits + totalDebits,
            SoldeFinal = compte.Solde,
            Ecritures = ecritures
        };
    }

    public async Task<decimal> GetSoldeAsync(int compteId)
    {
        var compte = await _context.Comptes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == compteId)
            ?? throw new InvalidOperationException(
                "Compte introuvable.");

        return compte.Solde;
    }

    public async Task<bool> VerifierEquilibreGlobalAsync()
    {
        var operationIncomplete = await _context.Operations
            .AsNoTracking()
            .Where(o => o.Statut == "Valide")
            .AnyAsync(o => o.Ecritures.Count() < 2);

        if (operationIncomplete)
        {
            return false;
        }

        var ecritureInvalide = await _context.Ecritures
            .AsNoTracking()
            .AnyAsync(e =>
                e.Montant <= 0m ||
                (e.Sens != "Debit" && e.Sens != "Credit"));

        if (ecritureInvalide)
        {
            return false;
        }

        var desequilibre = await _context.Ecritures
            .AsNoTracking()
            .GroupBy(e => new
            {
                e.OperationId,
                e.Devise
            })
            .Select(g => new
            {
                TotalDebit = g.Sum(e =>
                    e.Sens == "Debit" ? e.Montant : 0m),

                TotalCredit = g.Sum(e =>
                    e.Sens == "Credit" ? e.Montant : 0m)
            })
            .AnyAsync(x => x.TotalDebit != x.TotalCredit);

        return !desequilibre;
    }

    private async Task<Compte> ObtenirCompteCaisseAsync(
        int agenceId,
        string typeCaisse,
        string devise,
        DateTime date)
    {
        if (typeCaisse is not ("DEPOT" or "RETRAIT"))
        {
            throw new InvalidOperationException(
                "Le type de caisse est invalide.");
        }

        var numeroCompte =
            $"CAISSE-{typeCaisse}-{agenceId:D4}";

        var compteCaisse = await _context.Comptes
            .FirstOrDefaultAsync(c =>
                c.NumeroCompte == numeroCompte);

        if (compteCaisse is not null)
        {
            VerifierCompteActif(compteCaisse);

            if (!compteCaisse.EstInterne ||
                compteCaisse.ClientId.HasValue)
            {
                throw new InvalidOperationException(
                    $"Le compte {numeroCompte} n'est pas configuré comme compte interne.");
            }

            if (compteCaisse.Devise != devise)
            {
                throw new InvalidOperationException(
                    $"La devise du compte {numeroCompte} est invalide.");
            }

            return compteCaisse;
        }

        var typeCompte = await _context.TypesCompte
            .FirstOrDefaultAsync(t => t.Code == "COURANT");

        if (typeCompte is null)
        {
            throw new InvalidOperationException(
                "Le type de compte COURANT est introuvable.");
        }

        compteCaisse = new Compte
        {
            NumeroCompte = numeroCompte,
            ClientId = null,
            EstInterne = true,
            TypeCompteId = typeCompte.Id,
            AgenceId = agenceId,
            Solde = 0m,
            Devise = devise,
            Statut = "Actif",
            DateOuverture = date,
            DateCreation = date
        };

        _context.Comptes.Add(compteCaisse);
        await _context.SaveChangesAsync();

        return compteCaisse;
    }

    private async Task<Compte> ObtenirCompteCompensationAsync(
    int agenceId,
    string devise,
    DateTime date)
    {
        var numeroCompte = $"9000-{agenceId:D4}";

        var compte = await _context.Comptes
            .FirstOrDefaultAsync(c =>
                c.NumeroCompte == numeroCompte);

        if (compte is not null)
        {
            VerifierCompteActif(compte);

            if (!compte.EstInterne || compte.ClientId.HasValue)
            {
                throw new InvalidOperationException(
                    $"Le compte {numeroCompte} n'est pas configuré comme compte interne.");
            }

            if (compte.Devise != devise)
            {
                throw new InvalidOperationException(
                    $"La devise du compte {numeroCompte} est invalide.");
            }

            return compte;
        }

        var typeCompte = await _context.TypesCompte
            .FirstOrDefaultAsync(t => t.Code == "COURANT")
            ?? throw new InvalidOperationException(
                "Le type de compte COURANT est introuvable.");

        compte = new Compte
        {
            NumeroCompte = numeroCompte,
            ClientId = null,
            EstInterne = true,
            TypeCompteId = typeCompte.Id,
            AgenceId = agenceId,
            Solde = 0m,
            Devise = devise,
            Statut = "Actif",
            DateOuverture = date,
            DateCreation = date
        };

        _context.Comptes.Add(compte);
        await _context.SaveChangesAsync();

        return compte;
    }
    private async Task<Compte> ChargerCompteAsync(int compteId)
    {
        var compte = await _context.Comptes
            .FirstOrDefaultAsync(c => c.Id == compteId)
            ?? throw new InvalidOperationException(
                $"Compte introuvable : identifiant {compteId}.");

        await _context.Entry(compte).ReloadAsync();

        if (_context.Entry(compte).State == EntityState.Detached)
        {
            throw new InvalidOperationException(
                "Le compte n'existe plus.");
        }

        return compte;
    }

    private static void VerifierCompteActif(Compte compte)
    {
        if (compte.Statut != "Actif")
        {
            throw new InvalidOperationException(
                $"Compte {compte.Statut}. Opération impossible.");
        }
    }

    private async Task CreerAlertesSiNecessaireAsync(
        int clientId,
        int operationId,
        ResultatConformite analyse,
        bool fractionnement,
        ResultatSurveillance surveillance)
    {
        if (analyse.NecessiteDeclaration)
        {
            await _declaration.CreerAlerteAsync(
                clientId,
                operationId,
                "DepassementSeuil",
                analyse.Niveau.ToString(),
                analyse.Motif ?? "Dépassement du seuil AML.");
        }

        if (fractionnement)
        {
            await _declaration.CreerAlerteAsync(
                clientId,
                operationId,
                "Fractionnement",
                NiveauAlerte.Eleve.ToString(),
                "Suspicion de fractionnement : plusieurs opérations " +
                "sous le seuil sur une courte période.");
        }

        foreach (var alerte in surveillance.Alertes)
        {
            await _declaration.CreerAlerteAsync(
                clientId,
                operationId,
                alerte.Regle,
                alerte.Niveau.ToString(),
                alerte.Description);
        }
    }

    private static string GenererReference(string prefix)
    {
        var date = DateTime.UtcNow.ToString("yyyyMMdd");

        var suffixe = Guid.NewGuid()
            .ToString("N")[..16]
            .ToUpperInvariant();

        return $"{prefix}-{date}-{suffixe}";
    }

    private static void VerifierEquilibre(
        IEnumerable<Ecriture> ecritures)
    {
        var lignes = ecritures.ToList();

        if (lignes.Count < 2)
        {
            throw new InvalidOperationException(
                "Une opération doit avoir au moins deux écritures.");
        }

        if (lignes.Any(e =>
            e.Montant <= 0m ||
            (e.Sens != "Debit" && e.Sens != "Credit")))
        {
            throw new InvalidOperationException(
                "Une écriture contient un montant ou un sens invalide.");
        }

        foreach (var groupe in lignes.GroupBy(e => e.Devise))
        {
            var totalDebit = groupe
                .Where(e => e.Sens == "Debit")
                .Sum(e => e.Montant);

            var totalCredit = groupe
                .Where(e => e.Sens == "Credit")
                .Sum(e => e.Montant);

            if (totalDebit != totalCredit)
            {
                throw new InvalidOperationException(
                    $"Écritures déséquilibrées en {groupe.Key} : " +
                    $"débit {totalDebit:N2}, crédit {totalCredit:N2}.");
            }
        }
    }

    private static void ValiderEntree(
        int compteId,
        decimal montant,
        int utilisateurId,
        string? texte)
    {
        if (compteId <= 0)
        {
            throw new InvalidOperationException(
                "Un compte valide doit être sélectionné.");
        }

        if (utilisateurId <= 0)
        {
            throw new InvalidOperationException(
                "L'utilisateur est invalide.");
        }

        if (montant <= 0m)
        {
            throw new InvalidOperationException(
                "Le montant doit être strictement positif.");
        }

        if (montant > 9_999_999_999_999_999.99m)
        {
            throw new InvalidOperationException(
                "Le montant dépasse la capacité du champ decimal(18,2).");
        }

        if (decimal.Round(montant, 2) != montant)
        {
            throw new InvalidOperationException(
                "Le montant ne peut pas avoir plus de deux décimales.");
        }

        if (texte?.Trim().Length > 255)
        {
            throw new InvalidOperationException(
                "Le commentaire ou le motif ne peut pas dépasser " +
                "255 caractères.");
        }
    }
}