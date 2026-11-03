using System.Data;
using AkarangaBankMvc.Web.Data;
using AkarangaBankMvc.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace AkarangaBankMvc.Web.Services;

public class CompensationService : ICompensationService
{
    private readonly AkarangaBankContext _context;

    public CompensationService(AkarangaBankContext context)
    {
        _context = context;
    }

    public async Task<CompensationAgence> CalculerCompensationAsync(
        int agenceId,
        DateTime date,
        string devise = "BIF")
    {
        if (agenceId <= 0)
        {
            throw new InvalidOperationException(
                "Une agence valide doit être sélectionnée.");
        }

        devise = NormaliserDevise(devise);

        var debut = date.Date;

        if (debut == DateTime.MaxValue.Date)
        {
            throw new InvalidOperationException(
                "La date sélectionnée est invalide.");
        }

        var fin = debut.AddDays(1);

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        try
        {
            var numeroCompte = $"9000-{agenceId:D4}";

            var compte = await _context.Comptes
                .FirstOrDefaultAsync(c =>
                    c.NumeroCompte == numeroCompte &&
                    c.AgenceId == agenceId)
                ?? throw new InvalidOperationException(
                    $"Aucun compte de compensation pour l'agence {agenceId}. " +
                    "Vérifie qu'un virement inter-agences a été enregistré.");

            if (!compte.EstInterne || compte.ClientId.HasValue)
            {
                throw new InvalidOperationException(
                    $"Le compte {numeroCompte} doit être un compte interne.");
            }

            if (compte.Statut != "Actif")
            {
                throw new InvalidOperationException(
                    $"Le compte {numeroCompte} n'est pas actif.");
            }

            if (compte.Devise != devise)
            {
                throw new InvalidOperationException(
                    $"Le compte {numeroCompte} est en {compte.Devise}, " +
                    $"pas en {devise}.");
            }

            var compensation = await _context.CompensationsAgences
                .FirstOrDefaultAsync(c =>
                    c.AgenceId == agenceId &&
                    c.DateCompensation == debut &&
                    c.Devise == devise);

            if (compensation is not null &&
                (compensation.Statut == "Regle" ||
                 compensation.OperationReglementId.HasValue ||
                 compensation.DateReglement.HasValue))
            {
                throw new InvalidOperationException(
                    "Cette compensation est déjà réglée. " +
                    "Son calcul ne peut plus être modifié.");
            }

            var query = _context.Ecritures
                .AsNoTracking()
                .Where(e =>
                    e.CompteId == compte.Id &&
                    e.Devise == devise &&
                    e.DateEcriture >= debut &&
                    e.DateEcriture < fin &&
                    e.Operation.Statut == "Valide" &&
                    e.Operation.TypeOperation == "VirementInterAgences");

            var ecritureInvalide = await query.AnyAsync(e =>
                e.Montant <= 0m ||
                (e.Sens != "Debit" && e.Sens != "Credit"));

            if (ecritureInvalide)
            {
                throw new InvalidOperationException(
                    "Une écriture de compensation est invalide.");
            }

            var totaux = await query
                .GroupBy(e => e.CompteId)
                .Select(g => new
                {
                    Debits = g.Sum(e =>
                        e.Sens == "Debit" ? e.Montant : 0m),
                    Credits = g.Sum(e =>
                        e.Sens == "Credit" ? e.Montant : 0m)
                })
                .SingleOrDefaultAsync();

            var totalDebits = totaux?.Debits ?? 0m;
            var totalCredits = totaux?.Credits ?? 0m;
            var solde = totalCredits - totalDebits;

            const decimal maximum = 9_999_999_999_999_999.99m;

            if (totalDebits > maximum ||
                totalCredits > maximum ||
                Math.Abs(solde) > maximum)
            {
                throw new InvalidOperationException(
                    "Les totaux dépassent la capacité des champs comptables.");
            }

            if (compensation is null)
            {
                compensation = new CompensationAgence
                {
                    Reference =
                        $"COMP-{Guid.NewGuid():N}".Substring(0, 30),
                    AgenceId = agenceId,
                    CompteCompensationId = compte.Id,
                    Devise = devise,
                    DateCompensation = debut,
                    DateCreation = DateTime.UtcNow
                };

                _context.CompensationsAgences.Add(compensation);
            }
            else if (compensation.CompteCompensationId != compte.Id)
            {
                throw new InvalidOperationException(
                    "Le compte associé à cette compensation a changé.");
            }

            compensation.TotalDebits = totalDebits;
            compensation.TotalCredits = totalCredits;
            compensation.Solde = solde;

            compensation.Statut = solde == 0m
                ? "Equilibre"
                : "EnCours";

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return compensation;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<IEnumerable<CompensationAgence>>
        GetCompensationsAsync(
            int? agenceId = null,
            DateTime? date = null,
            string? devise = null)
    {
        var query = _context.CompensationsAgences
            .AsNoTracking()
            .Include(c => c.Agence)
            .Include(c => c.CompteCompensation)
            .Include(c => c.ReglePar)
            .Include(c => c.OperationReglement)
            .AsQueryable();

        if (agenceId.HasValue)
        {
            query = query.Where(c =>
                c.AgenceId == agenceId.Value);
        }

        if (date.HasValue)
        {
            var jour = date.Value.Date;

            query = query.Where(c =>
                c.DateCompensation == jour);
        }

        if (!string.IsNullOrWhiteSpace(devise))
        {
            var deviseNormalisee = NormaliserDevise(devise);

            query = query.Where(c =>
                c.Devise == deviseNormalisee);
        }

        return await query
            .OrderByDescending(c => c.DateCompensation)
            .ThenBy(c => c.AgenceId)
            .ThenBy(c => c.Devise)
            .ToListAsync();
    }

    public async Task<bool> VerifierEquilibreCompensationsAsync(
    DateTime date,
    string devise = "BIF")
    {
        devise = NormaliserDevise(devise);

        var debut = date.Date;

        if (debut == DateTime.MaxValue.Date)
        {
            throw new InvalidOperationException(
                "La date sélectionnée est invalide.");
        }

        var fin = debut.AddDays(1);

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        var query = _context.Ecritures
            .AsNoTracking()
            .Where(e =>
                e.Devise == devise &&
                e.DateEcriture >= debut &&
                e.DateEcriture < fin &&
                e.Operation.Statut == "Valide" &&
                e.Operation.TypeOperation == "VirementInterAgences" &&
                e.Compte.EstInterne &&
                e.Compte.NumeroCompte.StartsWith("9000-"));

        var ecritureInvalide = await query.AnyAsync(e =>
            e.Montant <= 0m ||
            (e.Sens != "Debit" && e.Sens != "Credit") ||
            e.Compte.ClientId.HasValue ||
            e.Compte.Devise != devise);

        if (ecritureInvalide)
        {
            return false;
        }

        var positions = await query
            .GroupBy(e => new
            {
                e.CompteId,
                e.Compte.AgenceId
            })
            .Select(g => new
            {
                g.Key.CompteId,
                g.Key.AgenceId,
                TotalDebits = g.Sum(e =>
                    e.Sens == "Debit" ? e.Montant : 0m),
                TotalCredits = g.Sum(e =>
                    e.Sens == "Credit" ? e.Montant : 0m)
            })
            .ToListAsync();

        var fiches = await _context.CompensationsAgences
            .AsNoTracking()
            .Where(c =>
                c.DateCompensation == debut &&
                c.Devise == devise)
            .ToListAsync();

        // Sans mouvement, il n'y a pas de compensation
        // à valider dans ce contrôle.
        if (positions.Count == 0)
        {
            return false;
        }

        foreach (var position in positions)
        {
            var fiche = fiches.SingleOrDefault(c =>
                c.AgenceId == position.AgenceId &&
                c.CompteCompensationId == position.CompteId);

            if (fiche is null)
            {
                return false;
            }

            if (fiche.TotalDebits != position.TotalDebits ||
                fiche.TotalCredits != position.TotalCredits ||
                fiche.Solde !=
                    position.TotalCredits - position.TotalDebits)
            {
                return false;
            }
        }

        foreach (var fiche in fiches)
        {
            var positionExiste = positions.Any(p =>
                p.AgenceId == fiche.AgenceId &&
                p.CompteId == fiche.CompteCompensationId);

            if (!positionExiste &&
                (fiche.TotalDebits != 0m ||
                 fiche.TotalCredits != 0m ||
                 fiche.Solde != 0m))
            {
                return false;
            }
        }

        var totalDebits = positions.Sum(p => p.TotalDebits);
        var totalCredits = positions.Sum(p => p.TotalCredits);

        await transaction.CommitAsync();

        return totalDebits == totalCredits;
    }

    public async Task<bool> ReglerCompensationsAsync(
    int compensationPositiveId,
    int compensationNegativeId,
    int utilisateurId)
    {
        if (compensationPositiveId <= 0 ||
            compensationNegativeId <= 0 ||
            compensationPositiveId == compensationNegativeId ||
            utilisateurId <= 0)
        {
            throw new InvalidOperationException(
                "Les identifiants du règlement sont invalides.");
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        try
        {
            var utilisateurExiste = await _context.Utilisateurs
                .AnyAsync(u => u.Id == utilisateurId && u.EstActif);

            if (!utilisateurExiste)
            {
                throw new InvalidOperationException(
                    "Utilisateur inexistant ou inactif.");
            }

            var fiches = await _context.CompensationsAgences
                .Where(c =>
                    c.Id == compensationPositiveId ||
                    c.Id == compensationNegativeId)
                .OrderBy(c => c.Id)
                .ToListAsync();

            if (fiches.Count != 2)
            {
                throw new InvalidOperationException(
                    "Une des compensations est introuvable.");
            }

            var positive = fiches.Single(c =>
                c.Id == compensationPositiveId);

            var negative = fiches.Single(c =>
                c.Id == compensationNegativeId);

            if (positive.Statut == "Regle" &&
                negative.Statut == "Regle" &&
                positive.OperationReglementId.HasValue &&
                positive.OperationReglementId ==
                    negative.OperationReglementId)
            {
                await transaction.CommitAsync();
                return false;
            }

            if (fiches.Any(c =>
                c.Statut == "Regle" ||
                c.OperationReglementId.HasValue ||
                c.DateReglement.HasValue))
            {
                throw new InvalidOperationException(
                    "Une compensation a déjà été réglée.");
            }

            if (positive.DateCompensation.Date !=
                    negative.DateCompensation.Date ||
                positive.Devise != negative.Devise ||
                positive.AgenceId == negative.AgenceId ||
                positive.CompteCompensationId ==
                    negative.CompteCompensationId)
            {
                throw new InvalidOperationException(
                    "Les compensations doivent concerner deux agences " +
                    "différentes, la même journée et la même devise.");
            }

            var debut = positive.DateCompensation.Date;

            if (debut >= DateTime.UtcNow.Date)
            {
                throw new InvalidOperationException(
                    "Le règlement est autorisé uniquement après la fin " +
                    "de la journée UTC concernée.");
            }

            var fin = debut.AddDays(1);

            if (positive.Solde <= 0m ||
                negative.Solde >= 0m ||
                positive.Solde != -negative.Solde)
            {
                throw new InvalidOperationException(
                    "Sélectionne une position positive et une position " +
                    "négative de même montant.");
            }

            var comptesIds = fiches
                .Select(c => c.CompteCompensationId)
                .ToArray();

            var comptes = await _context.Comptes
                .Where(c => comptesIds.Contains(c.Id))
                .OrderBy(c => c.Id)
                .ToListAsync();

            if (comptes.Count != 2)
            {
                throw new InvalidOperationException(
                    "Un compte de compensation est introuvable.");
            }

            foreach (var fiche in fiches)
            {
                var compte = comptes.Single(c =>
                    c.Id == fiche.CompteCompensationId);

                if (!compte.EstInterne ||
                    compte.ClientId.HasValue ||
                    compte.Statut != "Actif" ||
                    compte.AgenceId != fiche.AgenceId ||
                    compte.Devise != fiche.Devise ||
                    compte.NumeroCompte != $"9000-{fiche.AgenceId:D4}")
                {
                    throw new InvalidOperationException(
                        "La configuration d'un compte de compensation est invalide.");
                }

                var query = _context.Ecritures
                    .AsNoTracking()
                    .Where(e =>
                        e.CompteId == compte.Id &&
                        e.Devise == fiche.Devise &&
                        e.DateEcriture >= debut &&
                        e.DateEcriture < fin &&
                        e.Operation.Statut == "Valide" &&
                        e.Operation.TypeOperation == "VirementInterAgences");

                if (await query.AnyAsync(e =>
                    e.Montant <= 0m ||
                    (e.Sens != "Debit" && e.Sens != "Credit")))
                {
                    throw new InvalidOperationException(
                        "Une écriture de compensation est invalide.");
                }

                var debits = await query
                    .Where(e => e.Sens == "Debit")
                    .SumAsync(e => (decimal?)e.Montant) ?? 0m;

                var credits = await query
                    .Where(e => e.Sens == "Credit")
                    .SumAsync(e => (decimal?)e.Montant) ?? 0m;

                if (fiche.TotalDebits != debits ||
                    fiche.TotalCredits != credits ||
                    fiche.Solde != credits - debits)
                {
                    throw new InvalidOperationException(
                        "Le calcul a changé. Recalcule les compensations " +
                        "avant de les régler.");
                }
            }

            var comptePositif = comptes.Single(c =>
                c.Id == positive.CompteCompensationId);

            var compteNegatif = comptes.Single(c =>
                c.Id == negative.CompteCompensationId);

            var montant = positive.Solde;
            var maintenant = DateTime.UtcNow;

            const decimal maximum = 9_999_999_999_999_999.99m;

            if (Math.Abs(comptePositif.Solde - montant) > maximum ||
                Math.Abs(compteNegatif.Solde + montant) > maximum)
            {
                throw new InvalidOperationException(
                    "Le règlement dépasserait la capacité d'un solde.");
            }

            var operation = new Operation
            {
                Reference =
                    $"REG-{maintenant:yyyyMMdd}-" +
                    Guid.NewGuid().ToString("N")[..16].ToUpperInvariant(),
                TypeOperation = "ReglementCompensation",
                CompteId = comptePositif.Id,
                AgenceId = comptePositif.AgenceId,
                UtilisateurId = utilisateurId,
                Montant = montant,
                Devise = positive.Devise,
                Statut = "Valide",
                DateOperation = maintenant,
                DateValidation = maintenant,
                DateCreation = maintenant,
                Commentaire =
                    $"Règlement compensations {positive.Id} et {negative.Id}"
            };

            _context.Operations.Add(operation);
            await _context.SaveChangesAsync();

            _context.Ecritures.AddRange(
                new Ecriture
                {
                    OperationId = operation.Id,
                    CompteId = comptePositif.Id,
                    Sens = "Debit",
                    Montant = montant,
                    Devise = positive.Devise,
                    DateEcriture = maintenant,
                    Libelle =
                        $"Règlement de {positive.Reference}"
                },
                new Ecriture
                {
                    OperationId = operation.Id,
                    CompteId = compteNegatif.Id,
                    Sens = "Credit",
                    Montant = montant,
                    Devise = negative.Devise,
                    DateEcriture = maintenant,
                    Libelle =
                        $"Règlement de {negative.Reference}"
                });

            comptePositif.Solde -= montant;
            compteNegatif.Solde += montant;

            comptePositif.DateModification = maintenant;
            compteNegatif.DateModification = maintenant;

            foreach (var fiche in fiches)
            {
                fiche.Statut = "Regle";
                fiche.DateReglement = maintenant;
                fiche.RegleParId = utilisateurId;
                fiche.OperationReglementId = operation.Id;
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
    private static string NormaliserDevise(string devise)
    {
        if (string.IsNullOrWhiteSpace(devise))
        {
            throw new InvalidOperationException(
                "La devise est obligatoire.");
        }

        var resultat = devise.Trim().ToUpperInvariant();

        if (resultat.Length != 3 ||
            resultat.Any(c => c < 'A' || c > 'Z'))
        {
            throw new InvalidOperationException(
                "La devise doit contenir trois lettres, par exemple BIF.");
        }

        return resultat;
    }
}