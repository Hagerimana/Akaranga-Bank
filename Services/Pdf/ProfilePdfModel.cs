namespace AkarangaBankMvc.Web.Services.Pdf;

/// <summary>
/// Données utilisées pour générer la fiche PDF des informations personnelles.
/// </summary>
public class ProfilePdfModel
{
    public string NomComplet { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool EmailConfirme { get; set; }
    public string? Telephone { get; set; }
    public string RoleMetier { get; set; } = string.Empty;
    public string RolesIdentity { get; set; } = string.Empty;
    public string Agence { get; set; } = string.Empty;
    public DateTime DateCreation { get; set; }
    public DateTime? DerniereConnexion { get; set; }
    public bool DeuxFacteurs { get; set; }
    public string? ClientLie { get; set; }
    public DateTime GenereLe { get; set; } = DateTime.UtcNow;
}
