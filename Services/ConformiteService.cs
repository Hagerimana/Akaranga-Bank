using AkarangaBankMvc.Web.Data;
using AkarangaBankMvc.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace AkarangaBankMvc.Web.Services;

public class ConformiteService : IConformiteService
{
    private readonly AkarangaBankContext _context;
    private readonly ISeuilAMLService _seuils;

    public ConformiteService(
        AkarangaBankContext context,
        ISeuilAMLService seuils)
    {
        _context = context;
        _seuils = seuils;
    }

    public Task<decimal> GetSeuilAMLAsync()
    {
        return _seuils.GetSeuilVigilanceAsync();
    }

    public async Task<ResultatConformite> AnalyserTransactionAsync(
        int compteId,
        decimal montant,
        string typeOperation)
    {
        if (montant <= 0m ||
            montant > 9_999_999_999_999_999.99m ||
            decimal.Round(montant, 2) != montant)
        {
            throw new InvalidOperationException(
                "Le montant de l'analyse est invalide.");
        }

        typeOperation = NormaliserOperation(typeOperation);

        var compte = await _context.Comptes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == compteId)
            ?? throw new InvalidOperationException(
                "Compte introuvable.");

        var resultat = new ResultatConformite
        {
            CompteId = compteId,
            Montant = montant,
            DateAnalyse = DateTime.UtcNow,
            Niveau = NiveauAlerte.Faible
        };

        var seuilVigilance = await LireSeuilAsync(
            "VIGILANCE",
            typeOperation,
            compte.Devise);

        var seuilBlocage = await LireSeuilAsync(
            "BLOCAGE",
            typeOperation,
            compte.Devise);

        if (montant >= seuilVigilance)
        {
            resultat.NecessiteVigilanceRenforcee = true;
            resultat.Motif =
                $"Montant ≥ {seuilVigilance:N0} {compte.Devise}. " +
                "Revue de conformité et origine des fonds à vérifier.";
            resultat.Niveau = NiveauAlerte.Moyen;
        }

        if (typeOperation is "Depot" or "Retrait")
        {
            var seuilDeclaration = await LireSeuilAsync(
                "DECLARATION",
                typeOperation,
                compte.Devise);

            if (montant >= seuilDeclaration)
            {
                resultat.NecessiteDeclaration = true;
                resultat.Motif =
                    $"Transaction en espèces ≥ {seuilDeclaration:N0} " +
                    $"{compte.Devise}. Seuil de déclaration " +
                    "configuré pour l'exercice atteint.";
                resultat.Niveau = NiveauAlerte.Eleve;
            }
        }

        if (montant >= seuilBlocage)
        {
            resultat.EstBloquee = true;
            resultat.Motif =
                $"Opération bloquée : montant ≥ {seuilBlocage:N0} " +
                $"{compte.Devise}, seuil de blocage configuré.";
            resultat.Niveau = NiveauAlerte.Critique;
        }

        if ((typeOperation is "Retrait" or "Virement"
                or "VirementInterAgences") &&
            compte.Solde < montant)
        {
            resultat.EstBloquee = true;
            resultat.Motif = "Solde insuffisant.";
            resultat.Niveau = NiveauAlerte.Critique;
        }

        if (compte.Statut != "Actif")
        {
            resultat.EstBloquee = true;
            resultat.Motif =
                $"Compte {compte.Statut}. Opération impossible.";
            resultat.Niveau = NiveauAlerte.Critique;
        }

        return resultat;
    }

    public async Task<bool> DetecterFractionnementAsync(
        int compteId,
        decimal montant,
        DateTime date)
    {
        if (montant <= 0m)
        {
            throw new InvalidOperationException(
                "Le montant doit être positif.");
        }

        var compte = await _context.Comptes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == compteId)
            ?? throw new InvalidOperationException(
                "Compte introuvable.");

        var seuil = await LireSeuilAsync(
            "VIGILANCE",
            "Toutes",
            compte.Devise);

        if (montant >= seuil)
        {
            return false;
        }

        var debut = date.AddHours(-24);

        // Conserve le contrôle historique sur 24 heures,
        // mais ignore les écritures d'opérations non validées.
        var totalRecent = await _context.Ecritures
            .AsNoTracking()
            .Where(e =>
                e.CompteId == compteId &&
                e.Devise == compte.Devise &&
                e.Operation.Statut == "Valide" &&
                e.DateEcriture >= debut &&
                e.DateEcriture <= date &&
                e.Montant > 0m &&
                e.Montant < seuil &&
                (e.Sens == "Debit" || e.Sens == "Credit"))
            .SumAsync(e => (decimal?)e.Montant) ?? 0m;

        // Appel prévu avant l'enregistrement de l'opération courante.
        return totalRecent + montant >= seuil;
    }

    public async Task<NiveauRisqueClient> EvaluerRisqueClientAsync(
        int clientId)
    {
        var clientExiste = await _context.Clients
            .AsNoTracking()
            .AnyAsync(c => c.Id == clientId);

        if (!clientExiste)
        {
            return NiveauRisqueClient.Inconnu;
        }

        var score = 0;

        var nombreComptes = await _context.Comptes
            .AsNoTracking()
            .CountAsync(c =>
                c.ClientId == clientId &&
                !c.EstInterne);

        if (nombreComptes >= 3)
        {
            score += 2;
        }

        var devises = await _context.Comptes
            .AsNoTracking()
            .Where(c =>
                c.ClientId == clientId &&
                !c.EstInterne)
            .Select(c => c.Devise)
            .Distinct()
            .ToListAsync();

        var aTransactionsElevees = false;

        foreach (var devise in devises)
        {
            var seuil = await LireSeuilAsync(
                "VIGILANCE",
                "Toutes",
                devise);

            if (await _context.Ecritures
                .AsNoTracking()
                .AnyAsync(e =>
                    e.Compte.ClientId == clientId &&
                    !e.Compte.EstInterne &&
                    e.Devise == devise &&
                    e.Operation.Statut == "Valide" &&
                    e.Montant >= seuil))
            {
                aTransactionsElevees = true;
                break;
            }
        }

        if (aTransactionsElevees)
        {
            score += 3;
        }

        var aAlertes = await _context.AlertesAML
            .AsNoTracking()
            .AnyAsync(a => a.ClientId == clientId);

        if (aAlertes)
        {
            score += 5;
        }

        return score switch
        {
            >= 6 => NiveauRisqueClient.Eleve,
            >= 3 => NiveauRisqueClient.Moyen,
            _ => NiveauRisqueClient.Faible
        };
    }

    private async Task<decimal> LireSeuilAsync(
        string code,
        string typeOperation,
        string devise)
    {
        var seuil = await _seuils.GetSeuilAsync(
            code,
            typeOperation,
            devise);

        if (seuil is null)
        {
            throw new InvalidOperationException(
                $"Seuil AML actif manquant : {code}, " +
                $"{typeOperation}, {devise}. " +
                "Vérifie la configuration.");
        }

        return seuil.Montant;
    }

    private static string NormaliserOperation(string typeOperation)
    {
        return typeOperation?.Trim().ToUpperInvariant() switch
        {
            "DEPOT" => "Depot",
            "RETRAIT" => "Retrait",
            "VIREMENT" => "Virement",
            "VIREMENTINTERAGENCE" => "VirementInterAgences",
            "VIREMENTINTERAGENCES" => "VirementInterAgences",
            "PAIEMENT" => "Paiement",
            "ENLIGNE" => "EnLigne",
            "PAIEMENTENLIGNE" => "EnLigne",
            _ => throw new InvalidOperationException(
                "Type d'opération non pris en charge par ce contrôle AML.")
        };
    }
}