using AkarangaBankMvc.Web.Models;
using AkarangaBankMvc.Web.ViewModels;

namespace AkarangaBankMvc.Web.Services;

public interface IVirementInterAgenceService
{
    Task<VirementInterAgence> InitierAsync(
        VirementInterAgenceViewModel vm,
        int utilisateurId);

    Task<bool> ValiderAsync(
        int virementInterAgenceId,
        int valideParId);

    Task<bool> RejeterAsync(
        int virementInterAgenceId,
        int valideParId,
        string motif);

    Task<IEnumerable<VirementInterAgence>> GetEnAttenteAsync(
        int agenceId);

    Task<IEnumerable<VirementInterAgence>> GetHistoriqueAsync(
        int agenceId,
        DateTime? du,
        DateTime? au);
}