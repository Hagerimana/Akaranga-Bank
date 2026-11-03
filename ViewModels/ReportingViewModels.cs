using AkarangaBankMvc.Web.Models;

namespace AkarangaBankMvc.Web.ViewModels;

public class ReportingDashboardViewModel
{
    public decimal TotalDepots { get; set; }
    public decimal TotalRetraits { get; set; }
    public decimal TotalVirements { get; set; }
    public decimal TotalCreditsEnCours { get; set; }
    public int NombreClients { get; set; }
    public int NombreComptesActifs { get; set; }
    public int TotalAlertesAmlMois { get; set; }
    public int TotalDeclarationsCnrfMois { get; set; }
    public decimal VolumeTotalTransactions { get; set; }
    public bool EstBalanceEquilibree { get; set; }
}

public class JournalComptableViewModel
{
    public DateTime? DateDebut { get; set; }
    public DateTime? DateFin { get; set; }
    public string? TypeOperation { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public int TotalItems { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);

    public IEnumerable<JournalLigneViewModel> Lignes { get; set; } = new List<JournalLigneViewModel>();
    public decimal TotalDebits { get; set; }
    public decimal TotalCredits { get; set; }
    public bool EstEquilibre => Math.Abs(TotalDebits - TotalCredits) < 0.01m;
}

public class JournalLigneViewModel
{
    public int EcritureId { get; set; }
    public DateTime DateEcriture { get; set; }
    public string ReferenceOperation { get; set; } = string.Empty;
    public string TypeOperation { get; set; } = string.Empty;
    public string NumeroCompte { get; set; } = string.Empty;
    public string IntituleCompte { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}

public class BalanceGeneraleViewModel
{
    public DateTime DateArrete { get; set; } = DateTime.UtcNow;
    public List<BalanceLigneViewModel> Lignes { get; set; } = new();
    public decimal TotalDebits => Lignes.Sum(l => l.TotalDebits);
    public decimal TotalCredits => Lignes.Sum(l => l.TotalCredits);
    public decimal TotalSoldesDebiteurs => Lignes.Sum(l => l.SoldeDebiteur);
    public decimal TotalSoldesCrediteurs => Lignes.Sum(l => l.SoldeCrediteur);
    public bool EstEquilibree => Math.Abs(TotalDebits - TotalCredits) < 0.01m;
}

public class BalanceLigneViewModel
{
    public int CompteId { get; set; }
    public string NumeroCompte { get; set; } = string.Empty;
    public string Intitule { get; set; } = string.Empty;
    public string TypeCompte { get; set; } = string.Empty;
    public decimal TotalDebits { get; set; }
    public decimal TotalCredits { get; set; }
    public decimal SoldeDebiteur => TotalDebits > TotalCredits ? TotalDebits - TotalCredits : 0;
    public decimal SoldeCrediteur => TotalCredits > TotalDebits ? TotalCredits - TotalDebits : 0;
}

public class CompteResultatViewModel
{
    public DateTime DateDebut { get; set; }
    public DateTime DateFin { get; set; }

    public Dictionary<string, decimal> Produits { get; set; } = new();
    public Dictionary<string, decimal> Charges { get; set; } = new();

    public decimal TotalProduits => Produits.Values.Sum();
    public decimal TotalCharges => Charges.Values.Sum();
    public decimal ResultatNet => TotalProduits - TotalCharges;
}

public class BilanViewModel
{
    public DateTime DateArrete { get; set; } = DateTime.UtcNow;

    public Dictionary<string, decimal> PostesActif { get; set; } = new();
    public Dictionary<string, decimal> PostesPassif { get; set; } = new();

    public decimal TotalActif => PostesActif.Values.Sum();
    public decimal TotalPassif => PostesPassif.Values.Sum();
    public bool EstEquilibre => Math.Abs(TotalActif - TotalPassif) < 0.01m;
}

public class RapportAMLViewModel
{
    public int Annee { get; set; }
    public int Mois { get; set; }
    public int TotalAlertes { get; set; }
    public int AlertesCritiques { get; set; }
    public int AlertesElevees { get; set; }
    public int AlertesMoyennes { get; set; }
    public int AlertesFaibles { get; set; }
    public int AlertesTraitees { get; set; }
    public int DeclarationsCNRF { get; set; }
    public List<AlerteAML> Alertes { get; set; } = new();
}

