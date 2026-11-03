using AkarangaBankMvc.Web.Models;

namespace AkarangaBankMvc.Web.Services;

public interface ICompteInterneService
{
    /// <summary>
    /// Retourne le compte interne identifié par <paramref name="numeroCompte"/>,
    /// ou le crée s'il n'existe pas encore (contrepartie comptable).
    /// </summary>
    Task<Compte> ObtenirOuCreerAsync(
        int agenceId,
        string numeroCompte,
        string devise,
        DateTime date);
}
