namespace AkarangaBankMvc.Web.Models;

public class Carte
{
    public int Id { get; set; }
    public string NumeroCarte { get; set; } = string.Empty;
    public int CompteId { get; set; }
    public Compte Compte { get; set; } = null!;
    public int ClientId { get; set; }
    public Client Client { get; set; } = null!;
    public string TypeCarte { get; set; } = "Visa"; // Visa, MasterCard, Locale
    public DateTime DateExpiration { get; set; }
    public decimal PlafondRetrait { get; set; } = 1_000_000m;
    public decimal PlafondPaiement { get; set; } = 2_000_000m;
    public decimal PlafondLigne { get; set; } = 1_000_000m;
    public string Statut { get; set; } = "Active"; // Active, Bloquee, Expiree, Perdue, Volee, Opposition
    public string? MotifBlocage { get; set; }
    public DateTime DateCreation { get; set; } = DateTime.UtcNow;

    public ICollection<TransactionCarte> Transactions { get; set; }
        = new List<TransactionCarte>();

    public string NumeroMasque => string.IsNullOrEmpty(NumeroCarte) || NumeroCarte.Length < 4
        ? "****"
        : $"**** **** **** {NumeroCarte[^4..]}";
}