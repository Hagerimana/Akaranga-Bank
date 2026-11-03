using AkarangaBankMvc.Web.Models;

namespace AkarangaBankMvc.Web.Services;

public interface ICNRFService
{
    Task<DeclarationSoupcon> GenererDOSAsync(
        int alerteId,
        int redigeParId);

    Task<bool> ValiderAsync(
        int declarationId,
        int utilisateurId);

    // Simulation locale uniquement : aucun envoi externe.
    Task<bool> TransmettreAsync(int declarationId);

    // Enregistrement d'un accusé simulé.
    Task<bool> EnregistrerAccuseAsync(
        int declarationId,
        string reponse);

    Task<IEnumerable<DeclarationSoupcon>> GetDeclarationsAsync(
        string? statut);

    Task<string> GenererContenuDOSAsync(AlerteAML alerte);
}