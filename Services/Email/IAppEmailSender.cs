namespace AkarangaBankMvc.Web.Services.Email;

public interface IAppEmailSender
{
    /// <summary>
    /// Envoie un message HTML. Lève une exception explicite si la
    /// configuration SMTP est absente.
    /// </summary>
    Task SendEmailAsync(
        string destinataire,
        string sujet,
        string contenuHtml,
        CancellationToken cancellationToken = default);
}
