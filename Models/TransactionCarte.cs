namespace AkarangaBankMvc.Web.Models;

/// <summary>
/// Journal des autorisations de carte : conserve aussi bien les
/// transactions acceptées que les refus, avec leur motif (EF-12.13/12.14).
/// </summary>
public class TransactionCarte
{
    public int Id { get; set; }

    public int CarteId { get; set; }

    public Carte Carte { get; set; } = null!;

    public string Reference { get; set; } = string.Empty;

    /// <summary>Retrait, Paiement ou EnLigne.</summary>
    public string TypeTransaction { get; set; } = "Paiement";

    public decimal Montant { get; set; }

    public string? Commercant { get; set; }

    /// <summary>Autorisee ou Refusee.</summary>
    public string Statut { get; set; } = "Autorisee";

    public string? MotifRefus { get; set; }

    public DateTime DateTransaction { get; set; } = DateTime.UtcNow;

    public int? OperationId { get; set; }

    public Operation? Operation { get; set; }
}
