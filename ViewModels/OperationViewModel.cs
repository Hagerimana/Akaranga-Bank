using System.ComponentModel.DataAnnotations;

namespace AkarangaBankMvc.Web.ViewModels;

public class DepotViewModel
{
    [Required(ErrorMessage = "Le compte est obligatoire.")]
    [Range(1, int.MaxValue,
        ErrorMessage = "Veuillez sélectionner un compte valide.")]
    [Display(Name = "Compte")]
    public int CompteId { get; set; }

    [Required(ErrorMessage = "Le montant est obligatoire.")]
    [Range(typeof(decimal), "0.01", "9999999999999999.99",
        ParseLimitsInInvariantCulture = true,
        ErrorMessage = "Le montant doit être compris entre 0,01 et 9 999 999 999 999 999,99 BIF.")]
    [Display(Name = "Montant (BIF)")]
    public decimal Montant { get; set; }

    [StringLength(255,
        ErrorMessage = "Le commentaire ne peut pas dépasser 255 caractères.")]
    [Display(Name = "Commentaire")]
    public string? Commentaire { get; set; }
}

public class RetraitViewModel
{
    [Required(ErrorMessage = "Le compte est obligatoire.")]
    [Range(1, int.MaxValue,
        ErrorMessage = "Veuillez sélectionner un compte valide.")]
    [Display(Name = "Compte")]
    public int CompteId { get; set; }

    [Required(ErrorMessage = "Le montant est obligatoire.")]
    [Range(typeof(decimal), "0.01", "9999999999999999.99",
        ParseLimitsInInvariantCulture = true,
        ErrorMessage = "Le montant doit être compris entre 0,01 et 9 999 999 999 999 999,99 BIF.")]
    [Display(Name = "Montant (BIF)")]
    public decimal Montant { get; set; }

    [StringLength(255,
        ErrorMessage = "Le commentaire ne peut pas dépasser 255 caractères.")]
    [Display(Name = "Commentaire")]
    public string? Commentaire { get; set; }
}

public class VirementViewModel : IValidatableObject
{
    [Required(ErrorMessage = "Le compte source est obligatoire.")]
    [Range(1, int.MaxValue,
        ErrorMessage = "Veuillez sélectionner un compte source valide.")]
    [Display(Name = "Compte source")]
    public int CompteSourceId { get; set; }

    [Required(ErrorMessage = "Le compte destination est obligatoire.")]
    [Range(1, int.MaxValue,
        ErrorMessage = "Veuillez sélectionner un compte destination valide.")]
    [Display(Name = "Compte destination")]
    public int CompteDestinationId { get; set; }

    [Required(ErrorMessage = "Le montant est obligatoire.")]
    [Range(typeof(decimal), "0.01", "9999999999999999.99",
        ParseLimitsInInvariantCulture = true,
        ErrorMessage = "Le montant doit être compris entre 0,01 et 9 999 999 999 999 999,99 BIF.")]
    [Display(Name = "Montant (BIF)")]
    public decimal Montant { get; set; }

    [StringLength(255,
        ErrorMessage = "Le motif ne peut pas dépasser 255 caractères.")]
    [Display(Name = "Motif")]
    public string? Motif { get; set; }

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (CompteSourceId > 0 &&
            CompteDestinationId > 0 &&
            CompteSourceId == CompteDestinationId)
        {
            yield return new ValidationResult(
                "Le compte source et le compte destination doivent être différents.",
                new[] { nameof(CompteDestinationId) });
        }
    }
}
