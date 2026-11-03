// Chap 5
using System.ComponentModel.DataAnnotations;

namespace AkarangaBankMvc.Web.ViewModels;

// Données nécessaires pour créer une agence.
// Les Data Annotations définissent les règles de validation côté se rveur
// ET côté client (via jQuery Validation).
public class AgenceCreateViewModel
{
    [Required(ErrorMessage = "Le code de l'agence est obligatoire.")]
    [StringLength(20, MinimumLength = 3,
        ErrorMessage = "Le code doit contenir entre 3 et 20 caractères.")]
    [Display(Name = "Code agence")]
    [RegularExpression(@"^AKR-[A-Z]{3}$",
        ErrorMessage = "Le code doit respecter le format AKR-XXX (3 lettres majuscules).")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le nom est obligatoire.")]
    [StringLength(100, ErrorMessage = "Le nom ne peut pas dépasser 100 caractères.")]
    public string Nom { get; set; } = string.Empty;

    [Required(ErrorMessage = "La ville est obligatoire.")]
    [StringLength(50)]
    public string Ville { get; set; } = string.Empty;

    [Display(Name = "Est le siège")]
    public bool EstSiege { get; set; }
}