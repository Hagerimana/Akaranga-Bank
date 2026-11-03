using System.Data;
using System.Security.Cryptography;
using AkarangaBankMvc.Web.Data;
using AkarangaBankMvc.Web.Models;
using AkarangaBankMvc.Web.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AkarangaBankMvc.Web.Services;

/// <summary>
/// Gestion des cartes bancaires (chapitre 12) : émission, blocage,
/// renouvellement et autorisation de transactions.
/// </summary>
public class CarteService : ICarteService
{
    private static readonly string[] TypesAutorises =
        { "Visa", "MasterCard", "Locale" };

    private readonly AkarangaBankContext _context;
    private readonly ICompteInterneService _comptesInternes;
    private readonly IConformiteService _conformite;

    public CarteService(
        AkarangaBankContext context,
        ICompteInterneService comptesInternes,
        IConformiteService conformite)
    {
        _context = context;
        _comptesInternes = comptesInternes;
        _conformite = conformite;
    }

    // ============================================================
    // CONSULTATION
    // ============================================================
    public async Task<CarteListViewModel> GetPagedAsync(
        string? recherche,
        string? statut,
        int page,
        int taille)
    {
        page = Math.Max(1, page);
        taille = taille < 1 ? 10 : taille;

        var query = _context.Cartes
            .AsNoTracking()
            .Include(c => c.Client)
            .Include(c => c.Compte)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(recherche))
        {
            var r = recherche.Trim();

            query = query.Where(c =>
                c.NumeroCarte.Contains(r) ||
                c.Client.Nom.Contains(r) ||
                c.Client.Prenom.Contains(r) ||
                c.Compte.NumeroCompte.Contains(r));
        }

        if (!string.IsNullOrWhiteSpace(statut))
        {
            query = query.Where(c => c.Statut == statut);
        }

        var total = await query.CountAsync();

        var cartes = await query
            .OrderByDescending(c => c.DateCreation)
            .ThenByDescending(c => c.Id)
            .Skip((page - 1) * taille)
            .Take(taille)
            .ToListAsync();

        return new CarteListViewModel
        {
            Cartes = cartes,
            Recherche = recherche,
            Statut = statut,
            Page = page,
            TaillePage = taille,
            Total = total
        };
    }

    public async Task<CarteDetailsViewModel?> GetDetailsAsync(int id)
    {
        var carte = await _context.Cartes
            .AsNoTracking()
            .Include(c => c.Client)
            .Include(c => c.Compte)
            .Include(c => c.Compte.Agence)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (carte is null)
        {
            return null;
        }

        var transactions = await _context.TransactionsCartes
            .AsNoTracking()
            .Include(t => t.Operation)
            .Where(t => t.CarteId == id)
            .OrderByDescending(t => t.DateTransaction)
            .ThenByDescending(t => t.Id)
            .ToListAsync();

        return new CarteDetailsViewModel
        {
            Carte = carte,
            Transactions = transactions
        };
    }

    public async Task<IEnumerable<TransactionCarte>> GetHistoriqueAsync(
        int carteId)
    {
        return await _context.TransactionsCartes
            .AsNoTracking()
            .Where(t => t.CarteId == carteId)
            .OrderByDescending(t => t.DateTransaction)
            .ThenByDescending(t => t.Id)
            .ToListAsync();
    }

    // ============================================================
    // ÉMISSION (EF-12.9, EF-12.10)
    // ============================================================
    public async Task<Carte> EmettreAsync(CarteCreateViewModel vm)
    {
        ArgumentNullException.ThrowIfNull(vm);

        var type = NormaliserTypeCarte(vm.TypeCarte);

        var compte = await _context.Comptes
            .Include(c => c.Client)
            .FirstOrDefaultAsync(c => c.Id == vm.CompteId)
            ?? throw new InvalidOperationException(
                "Le compte associé est introuvable.");

        if (compte.EstInterne || !compte.ClientId.HasValue)
        {
            throw new InvalidOperationException(
                "Une carte ne peut être émise que pour un compte client.");
        }

        if (compte.Statut != "Actif")
        {
            throw new InvalidOperationException(
                $"Le compte est {compte.Statut} : émission impossible.");
        }

        if (vm.PlafondRetrait <= 0m ||
            vm.PlafondPaiement <= 0m ||
            vm.PlafondLigne <= 0m)
        {
            throw new InvalidOperationException(
                "Les plafonds doivent être strictement positifs.");
        }

        var date = DateTime.UtcNow;

        var carte = new Carte
        {
            NumeroCarte = await GenererNumeroUniqueAsync(type),
            CompteId = compte.Id,
            ClientId = compte.ClientId.Value,
            TypeCarte = type,
            DateExpiration = date.AddYears(3),
            PlafondRetrait = Arrondir(vm.PlafondRetrait),
            PlafondPaiement = Arrondir(vm.PlafondPaiement),
            PlafondLigne = Arrondir(vm.PlafondLigne),
            Statut = "Active",
            DateCreation = date
        };

        _context.Cartes.Add(carte);
        await _context.SaveChangesAsync();

        return carte;
    }

    // ============================================================
    // BLOCAGE (EF-12.11)
    // ============================================================
    public async Task<Carte> BloquerAsync(int carteId, string motif)
    {
        if (string.IsNullOrWhiteSpace(motif))
        {
            throw new InvalidOperationException(
                "Le motif de blocage est obligatoire.");
        }

        var motifNormalise = motif.Trim();

        var statut = motifNormalise.ToUpperInvariant() switch
        {
            "PERTE" => "Perdue",
            "VOL" => "Volee",
            "FRAUDE" => "Opposition",
            "AUTRE" => "Bloquee",
            _ => throw new InvalidOperationException(
                "Motif attendu : Perte, Vol, Fraude ou Autre.")
        };

        var carte = await _context.Cartes
            .FirstOrDefaultAsync(c => c.Id == carteId)
            ?? throw new InvalidOperationException(
                "La carte est introuvable.");

        if (carte.Statut == "Expiree")
        {
            throw new InvalidOperationException(
                "Une carte expirée ne peut pas être bloquée.");
        }

        carte.Statut = statut;
        carte.MotifBlocage = motifNormalise;

        await _context.SaveChangesAsync();

        return carte;
    }

    // ============================================================
    // RENOUVELLEMENT (EF-12.12)
    // ============================================================
    public async Task<Carte> RenouvelerAsync(int carteId)
    {
        var ancienne = await _context.Cartes
            .FirstOrDefaultAsync(c => c.Id == carteId)
            ?? throw new InvalidOperationException(
                "La carte est introuvable.");

        var date = DateTime.UtcNow;

        ancienne.Statut = "Expiree";

        var nouvelle = new Carte
        {
            NumeroCarte = await GenererNumeroUniqueAsync(ancienne.TypeCarte),
            CompteId = ancienne.CompteId,
            ClientId = ancienne.ClientId,
            TypeCarte = ancienne.TypeCarte,
            DateExpiration = date.AddYears(3),
            PlafondRetrait = ancienne.PlafondRetrait,
            PlafondPaiement = ancienne.PlafondPaiement,
            PlafondLigne = ancienne.PlafondLigne,
            Statut = "Active",
            DateCreation = date
        };

        _context.Cartes.Add(nouvelle);
        await _context.SaveChangesAsync();

        return nouvelle;
    }

    // ============================================================
    // AUTORISATION DE TRANSACTION (EF-12.13, EF-12.14)
    // ============================================================
    public async Task<TransactionCarte> AutoriserTransactionAsync(
        CarteTransactionViewModel vm,
        int utilisateurId)
    {
        ArgumentNullException.ThrowIfNull(vm);

        if (utilisateurId <= 0)
        {
            throw new InvalidOperationException(
                "Un utilisateur valide est obligatoire.");
        }

        var type = NormaliserTypeTransaction(vm.TypeTransaction);

        var montant = Arrondir(vm.Montant);

        if (montant <= 0m)
        {
            throw new InvalidOperationException(
                "Le montant de la transaction doit être positif.");
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        try
        {
            var carte = await _context.Cartes
                .Include(c => c.Compte)
                .FirstOrDefaultAsync(c => c.Id == vm.CarteId)
                ?? throw new InvalidOperationException(
                    "La carte est introuvable.");

            var compte = carte.Compte
                ?? throw new InvalidOperationException(
                    "Le compte associé à la carte est introuvable.");

            var date = DateTime.UtcNow;

            async Task<TransactionCarte> RefuserAsync(string motif)
            {
                var refus = new TransactionCarte
                {
                    CarteId = carte.Id,
                    Reference = GenererReference("CRT"),
                    TypeTransaction = type,
                    Montant = montant,
                    Commercant = NormaliserCommercant(vm.Commercant),
                    Statut = "Refusee",
                    MotifRefus = motif,
                    DateTransaction = date
                };

                _context.TransactionsCartes.Add(refus);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return refus;
            }

            if (carte.Statut != "Active")
            {
                return await RefuserAsync(
                    $"Carte {carte.Statut} : transaction refusée.");
            }

            if (carte.DateExpiration.Date < date.Date)
            {
                return await RefuserAsync("Carte expirée.");
            }

            if (compte.Statut != "Actif")
            {
                return await RefuserAsync(
                    $"Compte {compte.Statut} : transaction refusée.");
            }

            if (compte.Solde < montant)
            {
                return await RefuserAsync("Solde insuffisant.");
            }

            var plafond = type switch
            {
                "Retrait" => carte.PlafondRetrait,
                "Paiement" => carte.PlafondPaiement,
                "EnLigne" => carte.PlafondLigne,
                _ => 0m
            };

            if (plafond <= 0m)
            {
                return await RefuserAsync(
                    "Plafond journalier non configuré.");
            }

            var debutJournee = date.Date;
            var finJournee = debutJournee.AddDays(1);

            var dejaUtilise = await _context.TransactionsCartes
                .AsNoTracking()
                .Where(t =>
                    t.CarteId == carte.Id &&
                    t.Statut == "Autorisee" &&
                    t.TypeTransaction == type &&
                    t.DateTransaction >= debutJournee &&
                    t.DateTransaction < finJournee)
                .SumAsync(t => (decimal?)t.Montant) ?? 0m;

            if (dejaUtilise + montant > plafond)
            {
                return await RefuserAsync(
                    $"Plafond journalier « {type} » dépassé " +
                    $"({dejaUtilise + montant:N2} / {plafond:N2} BIF).");
            }

            var analyse = await _conformite.AnalyserTransactionAsync(
                compte.Id,
                montant,
                type);

            if (analyse.EstBloquee)
            {
                return await RefuserAsync(
                    "Contrôle AML : " +
                    (analyse.Motif ?? "transaction bloquée."));
            }

            var compteCartes = await _comptesInternes.ObtenirOuCreerAsync(
                compte.AgenceId,
                $"CARTES-{compte.AgenceId:D4}",
                compte.Devise,
                date);

            var operation = new Operation
            {
                Reference = GenererReference("AKR"),
                TypeOperation = type,
                CompteId = compte.Id,
                UtilisateurId = utilisateurId,
                AgenceId = compte.AgenceId,
                Montant = montant,
                Devise = compte.Devise,
                Statut = "Valide",
                DateOperation = date,
                DateValidation = date,
                DateCreation = date,
                Commentaire = ConstruireCommentaireCarte(
                    carte,
                    vm.Commercant,
                    analyse.Motif)
            };

            _context.Operations.Add(operation);
            await _context.SaveChangesAsync();

            var debit = new Ecriture
            {
                OperationId = operation.Id,
                CompteId = compte.Id,
                Sens = "Debit",
                Montant = montant,
                Devise = compte.Devise,
                DateEcriture = date,
                Libelle =
                    $"Carte {carte.NumeroMasque} — {type}"
            };

            var credit = new Ecriture
            {
                OperationId = operation.Id,
                CompteId = compteCartes.Id,
                Sens = "Credit",
                Montant = montant,
                Devise = compte.Devise,
                DateEcriture = date,
                Libelle =
                    $"Encaissement carte {carte.NumeroMasque}"
            };

            _context.Ecritures.AddRange(debit, credit);

            compte.Solde -= montant;
            compte.DateModification = date;

            compteCartes.Solde += montant;
            compteCartes.DateModification = date;

            var transactionCarte = new TransactionCarte
            {
                CarteId = carte.Id,
                Reference = GenererReference("CRT"),
                TypeTransaction = type,
                Montant = montant,
                Commercant = NormaliserCommercant(vm.Commercant),
                Statut = "Autorisee",
                DateTransaction = date,
                OperationId = operation.Id
            };

            _context.TransactionsCartes.Add(transactionCarte);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return transactionCarte;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // ============================================================
    // UTILITAIRES
    // ============================================================
    private async Task<string> GenererNumeroUniqueAsync(string type)
    {
        var prefixe = type switch
        {
            "Visa" => "4",
            "MasterCard" => "5",
            "Locale" => "6",
            _ => throw new InvalidOperationException(
                "Type de carte non pris en charge.")
        };

        for (var tentative = 0; tentative < 20; tentative++)
        {
            var chiffres = new char[16];
            chiffres[0] = prefixe[0];

            for (var i = 1; i < chiffres.Length; i++)
            {
                chiffres[i] = (char)('0' + RandomNumberGenerator.GetInt32(10));
            }

            var numero = new string(chiffres);

            var existe = await _context.Cartes
                .AsNoTracking()
                .AnyAsync(c => c.NumeroCarte == numero);

            if (!existe)
            {
                return numero;
            }
        }

        throw new InvalidOperationException(
            "Impossible de générer un numéro de carte unique.");
    }

    private static string NormaliserTypeCarte(string? typeCarte)
    {
        var resultat = typeCarte?.Trim() ?? string.Empty;

        var type = TypesAutorises.FirstOrDefault(t =>
            string.Equals(t, resultat, StringComparison.OrdinalIgnoreCase));

        return type ?? throw new InvalidOperationException(
            "Type de carte attendu : Visa, MasterCard ou Locale.");
    }

    private static string NormaliserTypeTransaction(string? type)
    {
        return type?.Trim().ToUpperInvariant() switch
        {
            "RETRAIT" => "Retrait",
            "PAIEMENT" => "Paiement",
            "ENLIGNE" => "EnLigne",
            "PAIEMENTENLIGNE" => "EnLigne",
            _ => throw new InvalidOperationException(
                "Type de transaction attendu : Retrait, Paiement ou EnLigne.")
        };
    }

    private static string? NormaliserCommercant(string? commercant)
    {
        if (string.IsNullOrWhiteSpace(commercant))
        {
            return null;
        }

        var valeur = commercant.Trim();

        return valeur.Length <= 100
            ? valeur
            : valeur[..100];
    }

    private static string ConstruireCommentaireCarte(
        Carte carte,
        string? commercant,
        string? motifAml)
    {
        var commentaire =
            $"Carte {carte.NumeroMasque}";

        if (!string.IsNullOrWhiteSpace(commercant))
        {
            commentaire += $" — {commercant.Trim()}";
        }

        if (!string.IsNullOrWhiteSpace(motifAml))
        {
            commentaire += $" — AML : {motifAml.Trim()}";
        }

        return commentaire.Length <= 255
            ? commentaire
            : commentaire[..255];
    }

    private static decimal Arrondir(decimal montant)
    {
        return decimal.Round(
            montant,
            2,
            MidpointRounding.AwayFromZero);
    }

    private static string GenererReference(string prefix)
    {
        var date = DateTime.UtcNow.ToString("yyyyMMdd");

        var suffixe = Guid.NewGuid()
            .ToString("N")[..16]
            .ToUpperInvariant();

        return $"{prefix}-{date}-{suffixe}";
    }
}
