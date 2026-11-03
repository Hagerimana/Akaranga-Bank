using AkarangaBankMvc.Web.Models;
using AkarangaBankMvc.Web.ViewModels;

namespace AkarangaBankMvc.Web.Services;

public interface IComptabiliteService
{
    Task<Operation> DeposerAsync(
        DepotViewModel vm,
        int utilisateurId);

    Task<Operation> RetirerAsync(
        RetraitViewModel vm,
        int utilisateurId);

    Task<Operation> VirerAsync(
        VirementViewModel vm,
        int utilisateurId);

    Task<JournalViewModel> GetJournalAsync(
        DateTime? du,
        DateTime? au,
        string? typeOperation,
        int page,
        int taille);

    Task<ReleveViewModel?> GetReleveAsync(int compteId);

    Task<decimal> GetSoldeAsync(int compteId);

    Task<bool> VerifierEquilibreGlobalAsync();
}