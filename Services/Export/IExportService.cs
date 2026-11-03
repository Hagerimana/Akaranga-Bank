using AkarangaBankMvc.Web.ViewModels;

namespace AkarangaBankMvc.Web.Services.Export;

public interface IExportService
{
    byte[] ExporterJournalCsv(JournalComptableViewModel model);
    byte[] ExporterBalanceCsv(BalanceGeneraleViewModel model);
}

