using AkarangaBankMvc.Web.Models;
using AkarangaBankMvc.Web.ViewModels;

namespace AkarangaBankMvc.Web.Services;

public interface ICarteService
{
    Task<CarteListViewModel> GetPagedAsync(
        string? recherche,
        string? statut,
        int page,
        int taille);

    Task<CarteDetailsViewModel?> GetDetailsAsync(int id);

    Task<Carte> EmettreAsync(CarteCreateViewModel vm);

    Task<Carte> BloquerAsync(int carteId, string motif);

    Task<Carte> RenouvelerAsync(int carteId);

    Task<TransactionCarte> AutoriserTransactionAsync(
        CarteTransactionViewModel vm,
        int utilisateurId);

    Task<IEnumerable<TransactionCarte>> GetHistoriqueAsync(int carteId);
}
