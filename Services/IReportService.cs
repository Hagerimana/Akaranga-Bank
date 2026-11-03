using AkarangaBankMvc.Web.ViewModels;

namespace AkarangaBankMvc.Web.Services;

public interface IReportService
{
    Task<ReportingDashboardViewModel> GetDashboardAsync();
    Task<JournalComptableViewModel> GetJournalAsync(DateTime? debut, DateTime? fin, string? typeOp, int page, int pageSize);
    Task<BalanceGeneraleViewModel> GetBalanceGeneraleAsync();
    Task<CompteResultatViewModel> GetCompteResultatAsync(DateTime debut, DateTime fin);
    Task<BilanViewModel> GetBilanAsync(DateTime? dateArrete = null);
    Task<RapportAMLViewModel> GetRapportAMLAsync(int annee, int mois);
}

