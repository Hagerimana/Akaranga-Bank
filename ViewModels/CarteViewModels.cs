using System.ComponentModel.DataAnnotations;
using AkarangaBankMvc.Web.Models;

namespace AkarangaBankMvc.Web.ViewModels;

public class CarteCreateViewModel
{
    [Required(ErrorMessage = "Le compte est obligatoire.")]
    [Display(Name = "Compte associé")]
    public int CompteId { get; set; }

    [Required]
    [Display(Name = "Réseau de carte")]
    public string TypeCarte { get; set; } = "Visa"; // Visa, MasterCard, Locale

    [Range(10_000, 50_000_000, ErrorMessage = "Le plafond de retrait doit être positif.")]
    [Display(Name = "Plafond journalier de retrait (BIF)")]
    public decimal PlafondRetrait { get; set; } = 1_000_000m;

    [Range(10_000, 50_000_000, ErrorMessage = "Le plafond de paiement doit être positif.")]
    [Display(Name = "Plafond journalier de paiement (BIF)")]
    public decimal PlafondPaiement { get; set; } = 2_000_000m;

    [Range(10_000, 50_000_000, ErrorMessage = "Le plafond en ligne doit être positif.")]
    [Display(Name = "Plafond en ligne (BIF)")]
    public decimal PlafondLigne { get; set; } = 1_000_000m;
}

public class CarteOppositionViewModel
{
    [Required]
    public int CarteId { get; set; }

    [Required(ErrorMessage = "Le motif est obligatoire.")]
    [Display(Name = "Motif d'opposition / blocage")]
    public string Motif { get; set; } = "Perte"; // Perte, Vol, Fraude, Autre

    [Required]
    [Display(Name = "Statut résultant")]
    public string NouveauStatut { get; set; } = "Opposition"; // Perdue, Volee, Opposition, Bloquee
}

public class CarteTransactionViewModel
{
    [Required]
    public int CarteId { get; set; }

    [Required]
    [Display(Name = "Type de transaction")]
    public string TypeTransaction { get; set; } = "Retrait"; // Retrait, Paiement, EnLigne

    [Required(ErrorMessage = "Le montant est obligatoire.")]
    [Range(1, double.MaxValue, ErrorMessage = "Le montant doit être supérieur à 0.")]
    [Display(Name = "Montant (BIF)")]
    public decimal Montant { get; set; }

    [Display(Name = "Commerçant / Guichet")]
    [StringLength(100)]
    public string? Commercant { get; set; }
}

public class CarteListViewModel
{
    public IEnumerable<Carte> Cartes { get; set; } = new List<Carte>();
    public string? Recherche { get; set; }
    public string? Statut { get; set; }
    public int Page { get; set; } = 1;
    public int TaillePage { get; set; } = 10;
    public int Total { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)Total / TaillePage);
}

public class CarteDetailsViewModel
{
    public Carte Carte { get; set; } = null!;
    public IEnumerable<TransactionCarte> Transactions { get; set; }
        = new List<TransactionCarte>();
}

