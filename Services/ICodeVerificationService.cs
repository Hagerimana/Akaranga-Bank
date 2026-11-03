using AkarangaBankMvc.Web.Models;

namespace AkarangaBankMvc.Web.Services;

public enum ResultatVerificationCode
{
    Reussi,
    CodeInvalide,
    Expire,
    TropDeTentatives,
    AucunCode
}

public interface ICodeVerificationService
{
    /// <summary>
    /// Génère un code à 6 chiffres, l'envoie par email puis le conserve
    /// (protégé) pendant 10 minutes.
    /// </summary>
    Task EnvoyerCodeAsync(
        ApplicationUser utilisateur,
        ObjectifCode objectif,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Vérifie le code saisi. Un code accepté est immédiatement consommé.
    /// </summary>
    Task<ResultatVerificationCode> VerifierCodeAsync(
        ApplicationUser utilisateur,
        ObjectifCode objectif,
        string code,
        CancellationToken cancellationToken = default);
}
