// Chap 6 
using System.ComponentModel.DataAnnotations;

namespace AkarangaBankMvc.Web.ViewModels;

public class CompteCreateViewModel
{
    [Required(ErrorMessage = "Le numéro de compte est obligatoire.")]
    [StringLength(30)]
    [Display(Name = "Numéro de compte")]
    [RegularExpression(@"^[A-Z]{3}-\d{4}-\d{4}$",
        ErrorMessage = "Le numéro doit respecter le format XXX-NNNN-NNNN.")]
    public string NumeroCompte { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le client est obligatoire.")]
    [Display(Name = "Client")]
    public int ClientId { get; set; }

    [Required(ErrorMessage = "Le type de compte est obligatoire.")]
    [Display(Name = "Type de compte")]
    public int TypeCompteId { get; set; }

    [Required(ErrorMessage = "L'agence est obligatoire.")]
    [Display(Name = "Agence")]
    public int AgenceId { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Le solde initial doit être positif ou nul.")]
    [Display(Name = "Solde initial (BIF)")]
    public decimal SoldeInitial { get; set; }
}