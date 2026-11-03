namespace AkarangaBankMvc.Web.Models;

/// <summary>
/// Objectif d'un code de vérification à 6 chiffres.
/// </summary>
public enum ObjectifCode
{
    ReinitialisationMotDePasse = 1,
    ChangementMotDePasse = 2
}

/// <summary>
/// Code de vérification à usage unique envoyé par email.
/// Le code lui-même n'est jamais stocké en clair : il est protégé par
/// l'API Data Protection d'ASP.NET Core.
/// </summary>
public class CodeVerification
{
    public int Id { get; set; }

    public int UtilisateurId { get; set; }
    public ApplicationUser Utilisateur { get; set; } = null!;

    /// <summary>Code chiffré via IDataProtector.</summary>
    public string CodeProtege { get; set; } = string.Empty;

    public ObjectifCode Objectif { get; set; }

    public DateTime CreeLe { get; set; } = DateTime.UtcNow;

    public DateTime ExpireLe { get; set; }

    public DateTime? UtiliseLe { get; set; }

    public int Tentatives { get; set; }
}
