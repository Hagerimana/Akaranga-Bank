using System.ComponentModel.DataAnnotations;

namespace AkarangaBankMvc.Web.ViewModels;

public class VirementInterAgenceViewModel
{
    [Range(1, int.MaxValue)]
    [Display(Name = "Compte source")]
    public int CompteSourceId { get; set; }

    [Range(1, int.MaxValue)]
    [Display(Name = "Compte destination")]
    public int CompteDestinationId { get; set; }

    [Range(typeof(decimal), "0.01", "9999999999999999.99",
        ParseLimitsInInvariantCulture = true)]
    public decimal Montant { get; set; }

    [StringLength(255)]
    public string? Motif { get; set; }
}
