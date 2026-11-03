using AkarangaBankMvc.Web.Models;
using AkarangaBankMvc.Web.ViewModels;

namespace AkarangaBankMvc.Web.Services;

public interface IPretService
{
    Task<PretListViewModel> GetPagedAsync(
        string? recherche,
        string? statut,
        int? typePretId,
        int page,
        int taille);

    Task<PretDetailsViewModel?> GetDetailsAsync(int id);

    Task<IEnumerable<TypePret>> GetTypesActifsAsync();

    Task<PretSimulationResult> SimulerAsync(
        int typePretId,
        decimal montant,
        int dureeMois);

    Task<Pret> DemanderAsync(
        PretDemandeViewModel vm,
        int utilisateurId);

    Task<Pret> ValiderAsync(
        int pretId,
        decimal tauxPersonnalise,
        int valideParId);

    Task<Pret> RejeterAsync(
        int pretId,
        string motif,
        int valideParId);

    Task<Pret> DebloquerAsync(int pretId, int utilisateurId);

    Task<Pret> RembourserAsync(
        int pretId,
        int echeanceId,
        int utilisateurId);

    Task<int> MettreAJourRetardsAsync(DateTime? dateReference = null);
}
