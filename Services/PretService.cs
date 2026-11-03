using System.Data;
using AkarangaBankMvc.Web.Data;
using AkarangaBankMvc.Web.Models;
using AkarangaBankMvc.Web.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AkarangaBankMvc.Web.Services;

/// <summary>
/// Cycle de vie d'un prêt (chapitre 12) :
/// demande → validation/rejet → déblocage → remboursement.
/// </summary>
public class PretService : IPretService
{
    private const decimal TauxPenaliteMensuel = 0.005m; // 0,5 %
    private const decimal PenaliteMinimum = 500m;

    private readonly AkarangaBankContext _context;
    private readonly ICompteInterneService _comptesInternes;

    public PretService(
        AkarangaBankContext context,
        ICompteInterneService comptesInternes)
    {
        _context = context;
        _comptesInternes = comptesInternes;
    }

    // ============================================================
    // CONSULTATION
    // ============================================================
    public async Task<PretListViewModel> GetPagedAsync(
        string? recherche,
        string? statut,
        int? typePretId,
        int page,
        int taille)
    {
        page = Math.Max(1, page);
        taille = taille < 1 ? 10 : taille;

        var query = _context.Prets
            .AsNoTracking()
            .Include(p => p.Client)
            .Include(p => p.CompteDebit)
            .Include(p => p.TypePret)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(recherche))
        {
            var r = recherche.Trim();

            query = query.Where(p =>
                p.Reference.Contains(r) ||
                p.Client.Nom.Contains(r) ||
                p.Client.Prenom.Contains(r) ||
                p.Client.NumeroClient.Contains(r));
        }

        if (!string.IsNullOrWhiteSpace(statut))
        {
            query = query.Where(p => p.Statut == statut);
        }

        if (typePretId.HasValue && typePretId.Value > 0)
        {
            query = query.Where(p => p.TypePretId == typePretId.Value);
        }

        var total = await query.CountAsync();

        var prets = await query
            .OrderByDescending(p => p.DateDemande)
            .ThenByDescending(p => p.Id)
            .Skip((page - 1) * taille)
            .Take(taille)
            .ToListAsync();

        var types = await _context.TypesPret
            .AsNoTracking()
            .OrderBy(t => t.Libelle)
            .ToListAsync();

        return new PretListViewModel
        {
            Prets = prets,
            TypesPret = types,
            Recherche = recherche,
            Statut = statut,
            TypePretId = typePretId,
            Page = page,
            TaillePage = taille,
            Total = total
        };
    }

    public async Task<PretDetailsViewModel?> GetDetailsAsync(int id)
    {
        var pret = await _context.Prets
            .AsNoTracking()
            .Include(p => p.Client)
            .Include(p => p.CompteDebit)
            .Include(p => p.TypePret)
            .Include(p => p.ValidePar)
            .Include(p => p.Echeances)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (pret is null)
        {
            return null;
        }

        return new PretDetailsViewModel
        {
            Pret = pret,
            Echeances = pret.Echeances
                .OrderBy(e => e.NumeroEcheance)
                .ToList()
        };
    }

    public async Task<IEnumerable<TypePret>> GetTypesActifsAsync()
    {
        return await _context.TypesPret
            .AsNoTracking()
            .Where(t => t.Actif)
            .OrderBy(t => t.Libelle)
            .ToListAsync();
    }

    // ============================================================
    // CALCULS (EF-12.3, ENF-12.5)
    // ============================================================
    public async Task<PretSimulationResult> SimulerAsync(
        int typePretId,
        decimal montant,
        int dureeMois)
    {
        var type = await _context.TypesPret
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == typePretId && t.Actif);

        if (type is null)
        {
            return new PretSimulationResult
            {
                Ok = false,
                Message = "Le type de prêt sélectionné est introuvable."
            };
        }

        if (montant < type.MontantMin || montant > type.MontantMax)
        {
            return new PretSimulationResult
            {
                Ok = false,
                Message =
                    $"Le montant doit être compris entre " +
                    $"{type.MontantMin:N0} et {type.MontantMax:N0} BIF " +
                    $"pour le type « {type.Libelle} »."
            };
        }

        if (dureeMois < type.DureeMinMois || dureeMois > type.DureeMaxMois)
        {
            return new PretSimulationResult
            {
                Ok = false,
                Message =
                    $"La durée doit être comprise entre " +
                    $"{type.DureeMinMois} et {type.DureeMaxMois} mois " +
                    $"pour le type « {type.Libelle} »."
            };
        }

        var taux = type.TauxMoyen;
        var mensualite = CalculerMensualite(montant, taux, dureeMois);

        return new PretSimulationResult
        {
            Ok = true,
            TauxInteret = taux,
            Mensualite = mensualite,
            TotalRembourse = decimal.Round(
                mensualite * dureeMois,
                2,
                MidpointRounding.AwayFromZero),
            TotalInterets = decimal.Round(
                mensualite * dureeMois - montant,
                2,
                MidpointRounding.AwayFromZero)
        };
    }

    /// <summary>
    /// Mensualité d'un prêt à amortissement constant.
    /// M = P × i / (1 − (1 + i)^−n), avec i = taux annuel / 12.
    /// </summary>
    public static decimal CalculerMensualite(
        decimal montant,
        decimal tauxAnnuel,
        int dureeMois)
    {
        ValiderParametresPret(montant, tauxAnnuel, dureeMois);

        montant = decimal.Round(
            montant,
            2,
            MidpointRounding.AwayFromZero);

        if (tauxAnnuel == 0m)
        {
            return decimal.Round(
                montant / dureeMois,
                2,
                MidpointRounding.AwayFromZero);
        }

        var tauxMensuel = tauxAnnuel / 100m / 12m;
        var facteur = Puissance(1m + tauxMensuel, dureeMois);

        var mensualite = montant * tauxMensuel * facteur / (facteur - 1m);

        return decimal.Round(
            mensualite,
            2,
            MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Génère l'échéancier. La dernière échéance absorbe les écarts
    /// d'arrondi : la somme des capitaux est exactement le principal.
    /// </summary>
    public static IReadOnlyList<EcheancePret> GenererEcheancier(
        decimal montant,
        decimal tauxAnnuel,
        int dureeMois,
        DateTime dateDebut)
    {
        ValiderParametresPret(montant, tauxAnnuel, dureeMois);

        montant = decimal.Round(
            montant,
            2,
            MidpointRounding.AwayFromZero);

        var mensualite = CalculerMensualite(
            montant,
            tauxAnnuel,
            dureeMois);

        var tauxMensuel = tauxAnnuel / 100m / 12m;
        var solde = montant;

        var echeances = new List<EcheancePret>(dureeMois);

        for (var numero = 1; numero <= dureeMois; numero++)
        {
            var interet = decimal.Round(
                solde * tauxMensuel,
                2,
                MidpointRounding.AwayFromZero);

            decimal capital;

            if (numero == dureeMois)
            {
                capital = solde;
            }
            else
            {
                capital = decimal.Round(
                    mensualite - interet,
                    2,
                    MidpointRounding.AwayFromZero);

                if (capital < 0m)
                {
                    capital = 0m;
                }

                if (capital > solde)
                {
                    capital = solde;
                }
            }

            var montantTotal = decimal.Round(
                capital + interet,
                2,
                MidpointRounding.AwayFromZero);

            solde = decimal.Round(
                solde - capital,
                2,
                MidpointRounding.AwayFromZero);

            if (numero == dureeMois)
            {
                solde = 0m;
            }

            echeances.Add(new EcheancePret
            {
                NumeroEcheance = numero,
                Capital = capital,
                Interet = interet,
                MontantTotal = montantTotal,
                SoldeRestant = solde,
                Penalite = 0m,
                DateEcheance = dateDebut.AddMonths(numero),
                Statut = "EnAttente"
            });
        }

        return echeances;
    }

    public static decimal CalculerPenalite(
        decimal montantTotal,
        DateTime dateEcheance,
        DateTime dateReference)
    {
        if (montantTotal <= 0m)
        {
            return 0m;
        }

        if (dateReference.Date <= dateEcheance.Date)
        {
            return 0m;
        }

        var mois = (dateReference.Year - dateEcheance.Year) * 12
            + dateReference.Month - dateEcheance.Month;

        if (dateReference.Day < dateEcheance.Day)
        {
            mois--;
        }

        mois = Math.Max(1, mois);

        var penalite = montantTotal * TauxPenaliteMensuel * mois;

        return Math.Max(
            PenaliteMinimum,
            decimal.Round(
                penalite,
                2,
                MidpointRounding.AwayFromZero));
    }

    // ============================================================
    // DEMANDE / VALIDATION / REJET (EF-12.1 à EF-12.4)
    // ============================================================
    public async Task<Pret> DemanderAsync(
        PretDemandeViewModel vm,
        int utilisateurId)
    {
        ArgumentNullException.ThrowIfNull(vm);

        if (utilisateurId <= 0)
        {
            throw new InvalidOperationException(
                "Un utilisateur valide est obligatoire.");
        }

        var type = await _context.TypesPret
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == vm.TypePretId && t.Actif)
            ?? throw new InvalidOperationException(
                "Le type de prêt sélectionné est introuvable ou inactif.");

        var client = await _context.Clients
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == vm.ClientId)
            ?? throw new InvalidOperationException(
                "Le client sélectionné est introuvable.");

        if (client.Statut != "Actif")
        {
            throw new InvalidOperationException(
                $"Le client est {client.Statut} : aucune demande possible.");
        }

        var compte = await _context.Comptes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == vm.CompteDebitId)
            ?? throw new InvalidOperationException(
                "Le compte de débit est introuvable.");

        if (compte.ClientId != vm.ClientId)
        {
            throw new InvalidOperationException(
                "Le compte de débit n'appartient pas au client sélectionné.");
        }

        if (compte.EstInterne || !compte.ClientId.HasValue)
        {
            throw new InvalidOperationException(
                "Le compte de débit doit être un compte client.");
        }

        if (compte.Statut != "Actif")
        {
            throw new InvalidOperationException(
                $"Le compte de débit est {compte.Statut}.");
        }

        if (vm.MontantPrincipal < type.MontantMin ||
            vm.MontantPrincipal > type.MontantMax)
        {
            throw new InvalidOperationException(
                $"Le montant doit être compris entre " +
                $"{type.MontantMin:N0} et {type.MontantMax:N0} BIF " +
                $"pour le type « {type.Libelle} ».");
        }

        if (vm.DureeMois < type.DureeMinMois ||
            vm.DureeMois > type.DureeMaxMois)
        {
            throw new InvalidOperationException(
                $"La durée doit être comprise entre " +
                $"{type.DureeMinMois} et {type.DureeMaxMois} mois " +
                $"pour le type « {type.Libelle} ».");
        }

        var montant = decimal.Round(
            vm.MontantPrincipal,
            2,
            MidpointRounding.AwayFromZero);

        var taux = type.TauxMoyen;

        var pret = new Pret
        {
            Reference = GenererReference("PRT"),
            ClientId = vm.ClientId,
            CompteDebitId = vm.CompteDebitId,
            TypePretId = type.Id,
            MontantPrincipal = montant,
            TauxInteret = taux,
            DureeMois = vm.DureeMois,
            Mensualite = CalculerMensualite(
                montant,
                taux,
                vm.DureeMois),
            MontantRembourse = 0m,
            SoldeRestant = montant,
            Statut = "EnAttente",
            Commentaire = string.IsNullOrWhiteSpace(vm.Commentaire)
                ? null
                : vm.Commentaire.Trim(),
            DateDemande = DateTime.UtcNow
        };

        _context.Prets.Add(pret);
        await _context.SaveChangesAsync();

        return pret;
    }

    public async Task<Pret> ValiderAsync(
        int pretId,
        decimal tauxPersonnalise,
        int valideParId)
    {
        if (pretId <= 0)
        {
            throw new InvalidOperationException(
                "Un prêt valide doit être sélectionné.");
        }

        if (tauxPersonnalise < 0m || tauxPersonnalise > 100m)
        {
            throw new InvalidOperationException(
                "Le taux d'intérêt doit être compris entre 0 et 100 %.");
        }

        var pret = await _context.Prets
            .Include(p => p.TypePret)
            .FirstOrDefaultAsync(p => p.Id == pretId)
            ?? throw new InvalidOperationException(
                "Le prêt est introuvable.");

        if (pret.Statut != "EnAttente")
        {
            throw new InvalidOperationException(
                $"Le prêt est déjà {pret.Statut} : validation impossible.");
        }

        pret.TauxInteret = decimal.Round(
            tauxPersonnalise,
            2,
            MidpointRounding.AwayFromZero);

        pret.Mensualite = CalculerMensualite(
            pret.MontantPrincipal,
            pret.TauxInteret,
            pret.DureeMois);

        pret.Statut = "Valide";
        pret.DateValidation = DateTime.UtcNow;

        if (valideParId > 0)
        {
            pret.ValideParId = valideParId;
        }

        await _context.SaveChangesAsync();

        return pret;
    }

    public async Task<Pret> RejeterAsync(
        int pretId,
        string motif,
        int valideParId)
    {
        if (string.IsNullOrWhiteSpace(motif))
        {
            throw new InvalidOperationException(
                "Le motif de rejet est obligatoire.");
        }

        motif = motif.Trim();

        if (motif.Length > 500)
        {
            throw new InvalidOperationException(
                "Le motif de rejet ne peut pas dépasser 500 caractères.");
        }

        var pret = await _context.Prets
            .FirstOrDefaultAsync(p => p.Id == pretId)
            ?? throw new InvalidOperationException(
                "Le prêt est introuvable.");

        if (pret.Statut != "EnAttente")
        {
            throw new InvalidOperationException(
                $"Le prêt est déjà {pret.Statut} : rejet impossible.");
        }

        pret.Statut = "Rejete";
        pret.MotifRejet = motif;
        pret.DateValidation = DateTime.UtcNow;

        if (valideParId > 0)
        {
            pret.ValideParId = valideParId;
        }

        await _context.SaveChangesAsync();

        return pret;
    }

    // ============================================================
    // DÉBLOCAGE (EF-12.5, ENF-12.1)
    // ============================================================
    public async Task<Pret> DebloquerAsync(int pretId, int utilisateurId)
    {
        if (utilisateurId <= 0)
        {
            throw new InvalidOperationException(
                "Un utilisateur valide est obligatoire.");
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        try
        {
            var pret = await _context.Prets
                .Include(p => p.CompteDebit)
                .Include(p => p.Echeances)
                .FirstOrDefaultAsync(p => p.Id == pretId)
                ?? throw new InvalidOperationException(
                    "Le prêt est introuvable.");

            if (pret.Statut != "Valide")
            {
                throw new InvalidOperationException(
                    $"Le prêt doit être validé avant son déblocage " +
                    $"(statut actuel : {pret.Statut}).");
            }

            if (pret.CompteDebit is null || pret.CompteDebit.EstInterne)
            {
                throw new InvalidOperationException(
                    "Le compte de débit du prêt est invalide.");
            }

            if (pret.CompteDebit.Statut != "Actif")
            {
                throw new InvalidOperationException(
                    $"Compte {pret.CompteDebit.Statut} : déblocage impossible.");
            }

            var date = DateTime.UtcNow;
            var montant = decimal.Round(
                pret.MontantPrincipal,
                2,
                MidpointRounding.AwayFromZero);

            var compte = pret.CompteDebit;

            var comptePret = await _comptesInternes.ObtenirOuCreerAsync(
                compte.AgenceId,
                $"PRETS-{compte.AgenceId:D4}",
                compte.Devise,
                date);

            var operation = new Operation
            {
                Reference = GenererReference("AKR"),
                TypeOperation = "DeblocagePret",
                CompteId = compte.Id,
                UtilisateurId = utilisateurId,
                AgenceId = compte.AgenceId,
                PretId = pret.Id,
                Montant = montant,
                Devise = compte.Devise,
                Statut = "Valide",
                DateOperation = date,
                DateValidation = date,
                DateCreation = date,
                Commentaire =
                    $"Déblocage du prêt {pret.Reference}"
            };

            _context.Operations.Add(operation);
            await _context.SaveChangesAsync();

            var debit = new Ecriture
            {
                OperationId = operation.Id,
                CompteId = comptePret.Id,
                Sens = "Debit",
                Montant = montant,
                Devise = compte.Devise,
                DateEcriture = date,
                Libelle =
                    $"Créance prêt {pret.Reference}"
            };

            var credit = new Ecriture
            {
                OperationId = operation.Id,
                CompteId = compte.Id,
                Sens = "Credit",
                Montant = montant,
                Devise = compte.Devise,
                DateEcriture = date,
                Libelle =
                    $"Déblocage prêt {pret.Reference} sur {compte.NumeroCompte}"
            };

            _context.Ecritures.AddRange(debit, credit);

            compte.Solde += montant;
            compte.DateModification = date;

            comptePret.Solde += montant;
            comptePret.DateModification = date;

            pret.Statut = "EnCours";
            pret.DateDebut = date;
            pret.DateFin = date.AddMonths(pret.DureeMois);
            pret.MontantRembourse = 0m;
            pret.SoldeRestant = montant;

            pret.Echeances.Clear();

            var echeances = GenererEcheancier(
                montant,
                pret.TauxInteret,
                pret.DureeMois,
                date);

            foreach (var echeance in echeances)
            {
                echeance.PretId = pret.Id;
                pret.Echeances.Add(echeance);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return pret;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // ============================================================
    // REMBOURSEMENT (EF-12.7)
    // ============================================================
    public async Task<Pret> RembourserAsync(
        int pretId,
        int echeanceId,
        int utilisateurId)
    {
        if (utilisateurId <= 0)
        {
            throw new InvalidOperationException(
                "Un utilisateur valide est obligatoire.");
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        try
        {
            var pret = await _context.Prets
                .Include(p => p.CompteDebit)
                .Include(p => p.Echeances)
                .FirstOrDefaultAsync(p => p.Id == pretId)
                ?? throw new InvalidOperationException(
                    "Le prêt est introuvable.");

            if (pret.Statut is not ("EnCours" or "Impaye"))
            {
                throw new InvalidOperationException(
                    $"Le prêt est {pret.Statut} : remboursement impossible.");
            }

            var echeance = pret.Echeances
                .FirstOrDefault(e => e.Id == echeanceId)
                ?? throw new InvalidOperationException(
                    "L'échéance est introuvable pour ce prêt.");

            if (echeance.Statut == "Payee")
            {
                throw new InvalidOperationException(
                    "Cette échéance est déjà payée.");
            }

            if (echeance.Statut == "Annulee")
            {
                throw new InvalidOperationException(
                    "Cette échéance est annulée.");
            }

            if (pret.CompteDebit is null || pret.CompteDebit.EstInterne)
            {
                throw new InvalidOperationException(
                    "Le compte de débit du prêt est invalide.");
            }

            var compte = pret.CompteDebit;

            if (compte.Statut != "Actif")
            {
                throw new InvalidOperationException(
                    $"Compte {compte.Statut} : remboursement impossible.");
            }

            var date = DateTime.UtcNow;

            echeance.Penalite = CalculerPenalite(
                echeance.MontantTotal,
                echeance.DateEcheance,
                date);

            var total = decimal.Round(
                echeance.MontantTotal + echeance.Penalite,
                2,
                MidpointRounding.AwayFromZero);

            if (compte.Solde < total)
            {
                throw new InvalidOperationException(
                    "Solde insuffisant pour rembourser cette échéance.");
            }

            var comptePret = await _comptesInternes.ObtenirOuCreerAsync(
                compte.AgenceId,
                $"PRETS-{compte.AgenceId:D4}",
                compte.Devise,
                date);

            var operation = new Operation
            {
                Reference = GenererReference("AKR"),
                TypeOperation = "RemboursementPret",
                CompteId = compte.Id,
                UtilisateurId = utilisateurId,
                AgenceId = compte.AgenceId,
                PretId = pret.Id,
                Montant = total,
                Devise = compte.Devise,
                Statut = "Valide",
                DateOperation = date,
                DateValidation = date,
                DateCreation = date,
                Commentaire =
                    $"Remboursement échéance n°{echeance.NumeroEcheance} " +
                    $"du prêt {pret.Reference}"
            };

            _context.Operations.Add(operation);
            await _context.SaveChangesAsync();

            var debit = new Ecriture
            {
                OperationId = operation.Id,
                CompteId = compte.Id,
                Sens = "Debit",
                Montant = total,
                Devise = compte.Devise,
                DateEcriture = date,
                Libelle =
                    $"Remboursement prêt {pret.Reference}, " +
                    $"échéance n°{echeance.NumeroEcheance}"
            };

            var credit = new Ecriture
            {
                OperationId = operation.Id,
                CompteId = comptePret.Id,
                Sens = "Credit",
                Montant = total,
                Devise = compte.Devise,
                DateEcriture = date,
                Libelle =
                    $"Encaissement remboursement {pret.Reference}"
            };

            _context.Ecritures.AddRange(debit, credit);

            compte.Solde -= total;
            compte.DateModification = date;

            comptePret.Solde -= total;
            comptePret.DateModification = date;

            echeance.Statut = "Payee";
            echeance.DatePaiement = date;

            pret.MontantRembourse += total;

            pret.SoldeRestant = decimal.Round(
                pret.Echeances
                    .Where(e => e.Statut != "Payee")
                    .Sum(e => e.Capital),
                2,
                MidpointRounding.AwayFromZero);

            if (pret.SoldeRestant == 0m ||
                pret.Echeances.All(e => e.Statut == "Payee"))
            {
                pret.SoldeRestant = 0m;
                pret.Statut = "Solde";
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return pret;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // ============================================================
    // RETARDS (EF-12.8)
    // ============================================================
    public async Task<int> MettreAJourRetardsAsync(
        DateTime? dateReference = null)
    {
        var reference = dateReference ?? DateTime.UtcNow;

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        try
        {
            var prets = await _context.Prets
                .Include(p => p.Echeances)
                .Where(p => p.Statut == "EnCours" || p.Statut == "Impaye")
                .ToListAsync();

            var nouvellesEnRetard = 0;

            foreach (var pret in prets)
            {
                foreach (var echeance in pret.Echeances)
                {
                    if (echeance.Statut == "Payee" ||
                        echeance.Statut == "Annulee")
                    {
                        continue;
                    }

                    if (reference.Date <= echeance.DateEcheance.Date)
                    {
                        continue;
                    }

                    if (echeance.Statut == "EnAttente")
                    {
                        echeance.Statut = "EnRetard";
                        nouvellesEnRetard++;
                    }

                    echeance.Penalite = CalculerPenalite(
                        echeance.MontantTotal,
                        echeance.DateEcheance,
                        reference);
                }

                var enRetard = pret.Echeances
                    .Count(e => e.Statut == "EnRetard");

                if (enRetard >= 3)
                {
                    pret.Statut = "Impaye";
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return nouvellesEnRetard;
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
    private static void ValiderParametresPret(
        decimal montant,
        decimal tauxAnnuel,
        int dureeMois)
    {
        if (montant <= 0m)
        {
            throw new InvalidOperationException(
                "Le montant du prêt doit être strictement positif.");
        }

        if (dureeMois <= 0 || dureeMois > 360)
        {
            throw new InvalidOperationException(
                "La durée doit être comprise entre 1 et 360 mois.");
        }

        if (tauxAnnuel < 0m || tauxAnnuel > 100m)
        {
            throw new InvalidOperationException(
                "Le taux d'intérêt doit être compris entre 0 et 100 %.");
        }
    }

    private static decimal Puissance(decimal valeur, int exposant)
    {
        var resultat = 1m;

        for (var i = 0; i < exposant; i++)
        {
            resultat *= valeur;
        }

        return resultat;
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
