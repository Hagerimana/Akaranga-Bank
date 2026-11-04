using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MimeKit;
using System.Net.Http.Json;
using AkarangaBankMvc.Web.Models;

namespace AkarangaBankMvc.Web.Services.Email;

/// <summary>
/// Envoi des emails transactionnels via SMTP (MailKit).
/// Implémente également <see cref="IEmailSender{TUser}"/> d'Identity.
/// </summary>
public class SmtpEmailSender : IAppEmailSender, IEmailSender<ApplicationUser>
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(
        IOptions<EmailOptions> options,
        ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(
        string destinataire,
        string sujet,
        string contenuHtml,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(destinataire))
        {
            throw new ArgumentException("L'adresse du destinataire est obligatoire.", nameof(destinataire));
        }

        if (!_options.EstConfigure)
        {
            throw new InvalidOperationException(
                "Configuration email incomplète. Renseignez Email:SmtpHost et Email:FromEmail " +
                "dans les user secrets du projet.");
        }

        if (_options.UtiliseApiHttp)
        {
            await SendViaBrevoApiAsync(destinataire, sujet, contenuHtml, cancellationToken);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromEmail));
        message.To.Add(MailboxAddress.Parse(destinataire));
        message.Subject = sujet;

        var builder = new BodyBuilder { HtmlBody = contenuHtml };
        message.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        client.Timeout = 20_000;

        var secureSocketOptions = !_options.EnableSsl
            ? SecureSocketOptions.None
            : _options.SmtpPort == 465
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTls;

        try
        {
            await client.ConnectAsync(
                _options.SmtpHost,
                _options.SmtpPort,
                secureSocketOptions,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(_options.SmtpUser))
            {
                await client.AuthenticateAsync(
                    _options.SmtpUser,
                    _options.SmtpPass,
                    cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("Email '{Sujet}' envoyé à {Destinataire}.", sujet, destinataire);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Échec de l'envoi de l'email '{Sujet}' à {Destinataire}.", sujet, destinataire);
            throw new InvalidOperationException(
                "L'envoi de l'email a échoué. Vérifiez la configuration SMTP.", ex);
        }
    }

    private async Task SendViaBrevoApiAsync(
        string destinataire,
        string sujet,
        string contenuHtml,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
            request.Headers.Add("api-key", _options.BrevoApiKey);
            request.Content = JsonContent.Create(new
            {
                sender = new { name = _options.FromName, email = _options.FromEmail },
                to = new[] { new { email = destinataire } },
                subject = sujet,
                htmlContent = contenuHtml
            });

            using var response = await Http.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException(
                    $"Brevo a répondu {(int)response.StatusCode} : {body}");
            }

            _logger.LogInformation("Email '{Sujet}' envoyé à {Destinataire} via l'API Brevo.", sujet, destinataire);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Échec de l'envoi de l'email '{Sujet}' à {Destinataire}.", sujet, destinataire);
            throw new InvalidOperationException(
                "L'envoi de l'email a échoué. Vérifiez la configuration email.", ex);
        }
    }

    public Task SendConfirmationLinkAsync(
        ApplicationUser user,
        string email,
        string confirmationLink)
        => SendEmailAsync(
            email,
            "Confirmez votre adresse email — Akaranga Bank",
            EmailTemplates.ConfirmationEmail(user.NomComplet, confirmationLink));

    public Task SendPasswordResetLinkAsync(
        ApplicationUser user,
        string email,
        string resetLink)
        => SendEmailAsync(
            email,
            "Réinitialisation du mot de passe — Akaranga Bank",
            EmailTemplates.ResetLinkEmail(user.NomComplet, resetLink));

    public Task SendPasswordResetCodeAsync(
        ApplicationUser user,
        string email,
        string resetCode)
        => SendEmailAsync(
            email,
            "Code de réinitialisation — Akaranga Bank",
            EmailTemplates.VerificationCodeEmail(user.NomComplet, resetCode, "réinitialiser votre mot de passe"));
}
