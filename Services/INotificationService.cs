using AkarangaBankMvc.Web.Models;
using AkarangaBankMvc.Web.ViewModels;

namespace AkarangaBankMvc.Web.Services;

public interface INotificationService
{
    Task<Notification> NotifierAsync(
        string code,
        string canal,
        string langue,
        int? destinataireId,
        string? email,
        string? telephone,
        IDictionary<string, string> variables);

    Task NotifierVirementRecuAsync(string email, string? telephone, string nomClient, string numeroCompte, decimal montant, string motif);
    Task NotifierPretValideAsync(string email, string? telephone, string nomClient, string referencePret, decimal montant);
    Task NotifierAlerteAMLCritiqueAsync(string numeroCompte, string motif, string niveauRisque);
    Task NotifierRappelEcheanceAsync(string email, string? telephone, string nomClient, decimal montant, DateTime dateEcheance);

    Task<int> ReessayerEchecsAsync(int maxTentatives = 3);
    Task<NotificationListViewModel> GetPagedAsync(string? canal, string? statut, int page, int pageSize);
    Task<NotificationDetailsViewModel?> GetDetailsAsync(int id);
}

