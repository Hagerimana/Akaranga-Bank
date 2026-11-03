using System.ComponentModel.DataAnnotations;
using AkarangaBankMvc.Web.Models;

namespace AkarangaBankMvc.Web.ViewModels;

public class ReleveViewModel
{
    public Compte Compte { get; set; } = null!;

    [Display(Name = "Solde initial")]
    public decimal SoldeInitial { get; set; }

    [Display(Name = "Solde final")]
    public decimal SoldeFinal { get; set; }

    public IEnumerable<Ecriture> Ecritures { get; set; }
        = Enumerable.Empty<Ecriture>();
}