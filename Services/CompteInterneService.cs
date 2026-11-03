using AkarangaBankMvc.Web.Data;
using AkarangaBankMvc.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace AkarangaBankMvc.Web.Services;

public class CompteInterneService : ICompteInterneService
{
    private readonly AkarangaBankContext _context;

    public CompteInterneService(AkarangaBankContext context)
    {
        _context = context;
    }

    public async Task<Compte> ObtenirOuCreerAsync(
        int agenceId,
        string numeroCompte,
        string devise,
        DateTime date)
    {
        if (agenceId <= 0)
        {
            throw new InvalidOperationException(
                "Une agence valide est obligatoire.");
        }

        numeroCompte = numeroCompte?.Trim().ToUpperInvariant()
            ?? string.Empty;

        if (numeroCompte.Length == 0 || numeroCompte.Length > 30)
        {
            throw new InvalidOperationException(
                "Le numéro du compte interne doit contenir de 1 à 30 caractères.");
        }

        devise = devise?.Trim().ToUpperInvariant() ?? string.Empty;

        if (devise.Length != 3)
        {
            throw new InvalidOperationException(
                "La devise du compte interne est invalide.");
        }

        var compte = await _context.Comptes
            .FirstOrDefaultAsync(c => c.NumeroCompte == numeroCompte);

        if (compte is not null)
        {
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

            if (compte.Statut != "Actif")
            {
                throw new InvalidOperationException(
                    $"Le compte interne {numeroCompte} est {compte.Statut}.");
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
}
