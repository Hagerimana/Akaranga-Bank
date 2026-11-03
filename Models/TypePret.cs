namespace AkarangaBankMvc.Web.Models;

/// <summary>
/// Produit de prêt : définit les bornes de montant, de durée et de taux
/// utilisées lors de la demande (chapitre 12, EF-12.2 et EF-12.3).
/// </summary>
public class TypePret
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Libelle { get; set; } = string.Empty;

    public decimal MontantMin { get; set; }

    public decimal MontantMax { get; set; }

    public int DureeMinMois { get; set; }

    public int DureeMaxMois { get; set; }

    public decimal TauxMin { get; set; }

    public decimal TauxMax { get; set; }

    public bool Actif { get; set; } = true;

    public DateTime DateCreation { get; set; } = DateTime.UtcNow;

    /// <summary>Taux appliqué par défaut : moyenne des taux min et max.</summary>
    public decimal TauxMoyen => decimal.Round(
        (TauxMin + TauxMax) / 2m,
        2,
        MidpointRounding.AwayFromZero);
}
