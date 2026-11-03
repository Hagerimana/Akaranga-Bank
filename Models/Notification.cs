using System.ComponentModel.DataAnnotations;

namespace AkarangaBankMvc.Web.Models;

/// <summary>
/// Journalisation et suivi des notifications envoyées (chapitre 14).
/// </summary>
public class Notification
{
    public int Id { get; set; }

    public int? DestinataireId { get; set; }
    public ApplicationUser? Destinataire { get; set; }

    [MaxLength(150)]
    public string? DestinataireEmail { get; set; }

    [MaxLength(50)]
    public string? DestinataireTelephone { get; set; }

    /// <summary>
    /// Email, SMS, Push, Interne
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Canal { get; set; } = "Email";

    [Required]
    [MaxLength(200)]
    public string Titre { get; set; } = string.Empty;

    [Required]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// EnAttente, Envoye, Echec
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Statut { get; set; } = "EnAttente";

    public DateTime DateCreation { get; set; } = DateTime.UtcNow;

    public DateTime? DateEnvoi { get; set; }

    public int NombreTentatives { get; set; } = 0;

    public string? DerniereErreur { get; set; }
}

