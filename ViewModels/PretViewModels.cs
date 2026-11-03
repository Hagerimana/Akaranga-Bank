using System.ComponentModel.DataAnnotations;
using AkarangaBankMvc.Web.Models;

namespace AkarangaBankMvc.Web.ViewModels;

public class PretDemandeViewModel
{
    [Required(ErrorMessage = "Le client est obligatoire.")]
    [Display(Name = "Client")]
    public int ClientId { get; set; }

    [Required(ErrorMessage = "Le compte de débit est obligatoire.")]
    [Display(Name = "Compte de débit")]
    public int CompteDebitId { get; set; }

    [Required(ErrorMessage = "Le type de prêt est obligatoire.")]
    [Display(Name = "Type de prêt")]
    public int TypePretId { get; set; }

    [Required(ErrorMessage = "Le montant principal est obligatoire.")]
    [Range(1, 500_000_000,
        ErrorMessage = "Le montant doit être compris entre 1 et 500 000 000 BIF.")]
    [Display(Name = "Montant principal (BIF)")]
    public decimal MontantPrincipal { get; set; }

    [Required(ErrorMessage = "La durée est obligatoire.")]
    [Range(1, 360,
        ErrorMessage = "La durée doit être comprise entre 1 et 360 mois.")]
    [Display(Name = "Durée (mois)")]
    public int DureeMois { get; set; } = 12;

    [StringLength(500)]
    [Display(Name = "Commentaire ou objet du prêt")]
    public string? Commentaire { get; set; }
}

public class PretValidationViewModel
{
    [Required]
    public int PretId { get; set; }

    [Display(Name = "Taux d'intérêt personnalisé (%)")]
    [Range(0, 100, ErrorMessage = "Le taux doit être entre 0 et 100 %.")]
    public decimal TauxInteret { get; set; }

    [Required]
    [Display(Name = "Décision")]
    public string Decision { get; set; } = "Valider"; // "Valider" ou "Rejeter"

    [Display(Name = "Motif de rejet (obligatoire en cas de rejet)")]
    [StringLength(500)]
    public string? MotifRejet { get; set; }
}

public class PretListViewModel
{
    public IEnumerable<Pret> Prets { get; set; } = new List<Pret>();
    public IEnumerable<TypePret> TypesPret { get; set; } = new List<TypePret>();
    public string? Recherche { get; set; }
    public string? Statut { get; set; }
    public int? TypePretId { get; set; }
    public int Page { get; set; } = 1;
    public int TaillePage { get; set; } = 10;
    public int Total { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)Total / TaillePage);
}

public class PretDetailsViewModel
{
    public Pret Pret { get; set; } = null!;
    public IEnumerable<EcheancePret> Echeances { get; set; } = new List<EcheancePret>();
    public decimal TotalCapital => Echeances.Sum(e => e.Capital);
    public decimal TotalInteret => Echeances.Sum(e => e.Interet);
    public decimal TotalMensualites => Echeances.Sum(e => e.MontantTotal);
    public int EcheancesPayees => Echeances.Count(e => e.Statut == "Payee");
    public int EcheancesEnRetard => Echeances.Count(e => e.Statut == "EnRetard");
}

/// <summary>Résultat renvoyé par l'endpoint de simulation (EF-12.3).</summary>
public class PretSimulationResult
{
    public bool Ok { get; set; }
    public string? Message { get; set; }
    public decimal TauxInteret { get; set; }
    public decimal Mensualite { get; set; }
    public decimal TotalInterets { get; set; }
    public decimal TotalRembourse { get; set; }
}
