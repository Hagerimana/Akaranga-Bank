using System.ComponentModel.DataAnnotations;

namespace AkarangaBankMvc.Web.Models;

/// <summary>
/// Modèle de notification multilingue (chapitre 14).
/// </summary>
public class NotificationTemplate
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Email, SMS, Push, Interne
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Canal { get; set; } = "Email";

    /// <summary>
    /// fr-BI, rn-BI, en-US, sw-KE
    /// </summary>
    [Required]
    [MaxLength(10)]
    public string Langue { get; set; } = "fr-BI";

    [Required]
    [MaxLength(200)]
    public string Sujet { get; set; } = string.Empty;

    public string ContenuHtml { get; set; } = string.Empty;

    [Required]
    public string ContenuTexte { get; set; } = string.Empty;

    public int Version { get; set; } = 1;

    public DateTime DateCreation { get; set; } = DateTime.UtcNow;
}

