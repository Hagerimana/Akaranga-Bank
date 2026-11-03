using System.Data;
using System.Globalization;
using AkarangaBankMvc.Web.Data;
using AkarangaBankMvc.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace AkarangaBankMvc.Web.Services;

public class SeuilAMLService : ISeuilAMLService
{
    private const decimal Maximum = 9_999_999_999_999_999.99m;

    private readonly AkarangaBankContext _context;

    public SeuilAMLService(AkarangaBankContext context)
    {
        _context = context;
    }

    public async Task<SeuilAML?> GetSeuilAsync(
        string code,
        string typeOperation,
        string devise = "BIF")
    {
        code = NormaliserCode(code);
        typeOperation = NormaliserTypeOperation(typeOperation);
        devise = NormaliserDevise(devise);

        var candidats = await _context.SeuilsAML
            .AsNoTracking()
            .Where(s =>
                s.Code == code &&
                s.Devise == devise &&
                s.Actif &&
                (s.TypeOperation == typeOperation ||
                 s.TypeOperation == "Toutes"))
            .ToListAsync();

        // Le seuil spécifique prime sur le seuil général.
        var specifiques = candidats
            .Where(s => s.TypeOperation == typeOperation)
            .ToList();

        var retenus = specifiques.Count > 0
            ? specifiques
            : candidats
                .Where(s => s.TypeOperation == "Toutes")
                .ToList();

        if (retenus.Count > 1)
        {
            throw new InvalidOperationException(
                $"Plusieurs seuils actifs correspondent à " +
                $"{code}, {typeOperation}, {devise}. " +
                "Corrige les doublons.");
        }

        var seuil = retenus.SingleOrDefault();

        if (seuil is not null)
        {
            ValiderMontant(seuil.Montant);
        }

        return seuil;
    }

    public async Task<IEnumerable<SeuilAML>> GetAllAsync()
    {
        return await _context.SeuilsAML
            .AsNoTracking()
            .OrderBy(s => s.Code)
            .ThenBy(s => s.Devise)
            .ThenBy(s => s.TypeOperation)
            .ThenBy(s => s.Id)
            .ToListAsync();
    }

    public async Task<bool> MettreAJourAsync(
        int seuilId,
        decimal nouveauMontant,
        int utilisateurId)
    {
        if (seuilId <= 0 || utilisateurId <= 0)
        {
            throw new InvalidOperationException(
                "Les identifiants sont invalides.");
        }

        ValiderMontant(nouveauMontant);

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        var utilisateurExiste = await _context.Utilisateurs
            .AnyAsync(u => u.Id == utilisateurId && u.EstActif);

        if (!utilisateurExiste)
        {
            throw new InvalidOperationException(
                "Utilisateur inexistant ou inactif.");
        }

        var seuil = await _context.SeuilsAML
            .FirstOrDefaultAsync(s => s.Id == seuilId);

        if (seuil is null)
        {
            return false;
        }

        await _context.Entry(seuil).ReloadAsync();

        var ancienMontant = seuil.Montant;

        if (ancienMontant == nouveauMontant)
        {
            await transaction.CommitAsync();
            return false;
        }

        var date = DateTime.UtcNow;

        seuil.Montant = nouveauMontant;
        seuil.DateModification = date;

        _context.AuditLogs.Add(new AuditLog
        {
            DateEvenement = date,
            UtilisateurId = utilisateurId,
            Action = "ModificationSeuilAML",
            TableConcernee = "SeuilsAML",
            EnregistrementId = seuil.Id,
            ValeurAvant = ancienMontant.ToString(
                "F2",
                CultureInfo.InvariantCulture),
            ValeurApres = nouveauMontant.ToString(
                "F2",
                CultureInfo.InvariantCulture),
            Details =
                $"Modification du seuil {seuil.Code}, " +
                $"opération {seuil.TypeOperation}, " +
                $"devise {seuil.Devise}."
        });

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return true;
    }

    public async Task<decimal> GetSeuilVigilanceAsync()
    {
        var seuil = await GetSeuilAsync(
            "VIGILANCE",
            "Toutes",
            "BIF");

        return seuil?.Montant ?? 5_000_000m;
    }

    public async Task<decimal> GetSeuilDeclarationAsync(
        string typeOperation = "Depot")
    {
        var seuil = await GetSeuilAsync(
            "DECLARATION",
            typeOperation,
            "BIF");

        return seuil?.Montant ?? 10_000_000m;
    }

    public async Task<decimal> GetSeuilBlocageAsync()
    {
        var seuil = await GetSeuilAsync(
            "BLOCAGE",
            "Toutes",
            "BIF");

        return seuil?.Montant ?? 50_000_000m;
    }

    private static void ValiderMontant(decimal montant)
    {
        if (montant <= 0m ||
            montant > Maximum ||
            decimal.Round(montant, 2) != montant)
        {
            throw new InvalidOperationException(
                "Le seuil doit être strictement positif, " +
                "respecter la capacité decimal(18,2) " +
                "et avoir au maximum deux décimales.");
        }
    }

    private static string NormaliserCode(string code)
    {
        var resultat = code?.Trim().ToUpperInvariant()
            ?? string.Empty;

        if (resultat is not
            ("VIGILANCE" or "DECLARATION" or "BLOCAGE"))
        {
            throw new InvalidOperationException(
                "Code attendu : VIGILANCE, DECLARATION ou BLOCAGE.");
        }

        return resultat;
    }

    private static string NormaliserTypeOperation(string typeOperation)
    {
        var resultat = typeOperation?.Trim() ?? string.Empty;

        if (resultat.Length == 0 || resultat.Length > 30)
        {
            throw new InvalidOperationException(
                "Le type d'opération doit contenir de 1 à 30 caractères.");
        }

        return resultat.ToUpperInvariant() switch
        {
            "TOUTES" => "Toutes",
            "DEPOT" => "Depot",
            "RETRAIT" => "Retrait",
            "VIREMENT" => "Virement",
            "VIREMENTINTERAGENCES" => "VirementInterAgences",
            _ => resultat
        };
    }

    private static string NormaliserDevise(string devise)
    {
        var resultat = devise?.Trim().ToUpperInvariant()
            ?? string.Empty;

        if (resultat.Length != 3 ||
            resultat.Any(c => c < 'A' || c > 'Z'))
        {
            throw new InvalidOperationException(
                "La devise doit contenir trois lettres, par exemple BIF.");
        }

        return resultat;
    }
}