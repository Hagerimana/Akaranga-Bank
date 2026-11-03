using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AkarangaBankMvc.Web.Models;

/// <summary>
/// Document archivé ou pièce justificative (chapitre 14).
/// Obligation de conservation de 5 à 10 ans (LBC/FT).
/// </summary>
public class Document
{
    public int Id { get; set; }

    [Required]
    [MaxLength(150)]
    public string Titre { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string NomFichierOriginal { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string NomFichierStocke { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string CheminRelatif { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string ContentType { get; set; } = "application/octet-stream";

    [Required]
    [MaxLength(20)]
    public string Extension { get; set; } = string.Empty;

    public long TailleOctets { get; set; }

    [Required]
    [MaxLength(64)]
    public string Sha256Hash { get; set; } = string.Empty;

    /// <summary>
    /// Catégories : KYC, Contrat, Comptable, AML, RH, Audit
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Categorie { get; set; } = "KYC";

    /// <summary>
    /// Statuts : Actif, Archive, Supprime
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Statut { get; set; } = "Actif";

    public int? ClientId { get; set; }
    public Client? Client { get; set; }

    public int? CreeParUtilisateurId { get; set; }
    public ApplicationUser? CreeParUtilisateur { get; set; }

    public DateTime DateCreation { get; set; } = DateTime.UtcNow;

    public DateTime? DateSuppression { get; set; }

    public ICollection<DocumentAcces> Acces { get; set; } = new List<DocumentAcces>();
}

