using System.ComponentModel.DataAnnotations;

namespace AkarangaBankMvc.Web.Models;

/// <summary>
/// Traçabilité des accès aux documents (chapitre 14).
/// </summary>
public class DocumentAcces
{
    public int Id { get; set; }

    public int DocumentId { get; set; }
    public Document Document { get; set; } = null!;

    public int? UtilisateurId { get; set; }
    public ApplicationUser? Utilisateur { get; set; }

    /// <summary>
    /// Consultation, Telechargement, Archivage, Suppression
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string TypeAcces { get; set; } = "Consultation";

    public DateTime DateAcces { get; set; } = DateTime.UtcNow;

    [MaxLength(50)]
    public string? AdresseIp { get; set; }
}

