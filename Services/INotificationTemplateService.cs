using AkarangaBankMvc.Web.Models;

namespace AkarangaBankMvc.Web.Services;

public interface INotificationTemplateService
{
    Task<NotificationTemplate?> GetTemplateAsync(string code, string canal, string langue);
    Task<(string Sujet, string MessageHtml, string MessageTexte)> RenderAsync(
        string code,
        string canal,
        string langue,
        IDictionary<string, string> variables);
}

