namespace AkarangaBankMvc.Web.Models;

public class CompensationAgence
{
    public int Id { get; set; }

    public string Reference { get; set; } = string.Empty;

    public int AgenceId { get; set; }
    public Agence Agence { get; set; } = null!;

    public int CompteCompensationId { get; set; }
    public Compte CompteCompensation { get; set; } = null!;

    public string Devise { get; set; } = "BIF";

    // Totaux des écritures du compte de compensation
    // pour la journée concernée.
    public decimal TotalDebits { get; set; }
    public decimal TotalCredits { get; set; }

    // Convention : TotalCredits - TotalDebits.
    public decimal Solde { get; set; }

    // Journée comptable concernée.
    public DateTime DateCompensation { get; set; }

    public string Statut { get; set; } = "EnCours";

    public DateTime DateCreation { get; set; } = DateTime.UtcNow;

    public DateTime? DateReglement { get; set; }

    public int? RegleParId { get; set; }
    public ApplicationUser? ReglePar { get; set; }

    public int? OperationReglementId { get; set; }
    public Operation? OperationReglement { get; set; }
}