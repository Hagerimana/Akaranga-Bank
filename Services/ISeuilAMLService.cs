using AkarangaBankMvc.Web.Models;

namespace AkarangaBankMvc.Web.Services;

public interface ISeuilAMLService
{
    Task<SeuilAML?> GetSeuilAsync(
        string code,
        string typeOperation,
        string devise = "BIF");

    Task<IEnumerable<SeuilAML>> GetAllAsync();

    Task<bool> MettreAJourAsync(
        int seuilId,
        decimal nouveauMontant,
        int utilisateurId);

    Task<decimal> GetSeuilVigilanceAsync();

    Task<decimal> GetSeuilDeclarationAsync(
        string typeOperation = "Depot");

    Task<decimal> GetSeuilBlocageAsync();
}