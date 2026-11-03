using AkarangaBankMvc.Web.Models;

namespace AkarangaBankMvc.Web.Services;

public interface ICompensationService
{
    Task<CompensationAgence> CalculerCompensationAsync(
        int agenceId,
        DateTime date,
        string devise = "BIF");

    Task<IEnumerable<CompensationAgence>> GetCompensationsAsync(
        int? agenceId = null,
        DateTime? date = null,
        string? devise = null);

    Task<bool> VerifierEquilibreCompensationsAsync(
        DateTime date,
        string devise = "BIF");

    Task<bool> ReglerCompensationsAsync(
        int compensationPositiveId,
        int compensationNegativeId,
        int utilisateurId);
}