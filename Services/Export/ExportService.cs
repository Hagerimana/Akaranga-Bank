using System.Text;
using AkarangaBankMvc.Web.ViewModels;

namespace AkarangaBankMvc.Web.Services.Export;

public class ExportService : IExportService
{
    public byte[] ExporterJournalCsv(JournalComptableViewModel model)
    {
        var sb = new StringBuilder();
        // BOM UTF-8 pour ouverture directe dans Excel
        sb.Append('\uFEFF');

        // En-tête
        sb.AppendLine("ID;Date;Référence;Type;Compte;Intitulé;Libellé;Débit (BIF);Crédit (BIF)");

        foreach (var l in model.Lignes)
        {
            sb.AppendLine($"{l.EcritureId};{l.DateEcriture:yyyy-MM-dd HH:mm};\"{Nettoyer(l.ReferenceOperation)}\";\"{Nettoyer(l.TypeOperation)}\";\"{Nettoyer(l.NumeroCompte)}\";\"{Nettoyer(l.IntituleCompte)}\";\"{Nettoyer(l.Libelle)}\";{l.Debit:F2};{l.Credit:F2}");
        }

        // Totaux
        sb.AppendLine($";;;;;;TOTAL;{model.TotalDebits:F2};{model.TotalCredits:F2}");

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public byte[] ExporterBalanceCsv(BalanceGeneraleViewModel model)
    {
        var sb = new StringBuilder();
        sb.Append('\uFEFF');

        sb.AppendLine("N° Compte;Intitulé;Type Compte;Total Débits (BIF);Total Crédits (BIF);Solde Débiteur (BIF);Solde Créditeur (BIF)");

        foreach (var l in model.Lignes)
        {
            sb.AppendLine($"\"{Nettoyer(l.NumeroCompte)}\";\"{Nettoyer(l.Intitule)}\";\"{Nettoyer(l.TypeCompte)}\";{l.TotalDebits:F2};{l.TotalCredits:F2};{l.SoldeDebiteur:F2};{l.SoldeCrediteur:F2}");
        }

        sb.AppendLine($";TOTAL;;{model.TotalDebits:F2};{model.TotalCredits:F2};{model.TotalSoldesDebiteurs:F2};{model.TotalSoldesCrediteurs:F2}");

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string Nettoyer(string? texte)
    {
        if (string.IsNullOrEmpty(texte)) return string.Empty;
        return texte.Replace("\"", "\"\"");
    }
}

