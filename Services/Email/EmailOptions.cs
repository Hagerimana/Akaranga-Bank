namespace AkarangaBankMvc.Web.Services.Email;

/// <summary>
/// Configuration SMTP de l'application. Les valeurs sensibles doivent être
/// fournies via les user secrets ou les variables d'environnement.
/// </summary>
public class EmailOptions
{
    public const string SectionName = "Email";

    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string SmtpUser { get; set; } = string.Empty;
    public string SmtpPass { get; set; } = string.Empty;
    public string FromName { get; set; } = "Akaranga Bank";
    public string FromEmail { get; set; } = string.Empty;

    public bool EstConfigure => !string.IsNullOrWhiteSpace(SmtpHost)
        && !string.IsNullOrWhiteSpace(FromEmail);
}
