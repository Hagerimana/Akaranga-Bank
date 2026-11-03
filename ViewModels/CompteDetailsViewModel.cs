// Chap 6
using AkarangaBankMvc.Web.Models;
namespace AkarangaBankMvc.Web.ViewModels;

public class CompteDetailsViewModel
{
    public Compte Compte { get; set; } = null!;
    public IEnumerable<Ecriture> Ecritures { get; set; } = new
   List<Ecriture>();
}