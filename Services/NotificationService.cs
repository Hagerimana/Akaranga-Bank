using AkarangaBankMvc.Web.Data;
using AkarangaBankMvc.Web.Hubs;
using AkarangaBankMvc.Web.Models;
using AkarangaBankMvc.Web.Services.Email;
using AkarangaBankMvc.Web.ViewModels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AkarangaBankMvc.Web.Services;

public class NotificationService : INotificationService
{
    private readonly AkarangaBankContext _context;
    private readonly INotificationTemplateService _templateService;
    private readonly IAppEmailSender _emailSender;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        AkarangaBankContext context,
        INotificationTemplateService templateService,
        IAppEmailSender emailSender,
        IHubContext<NotificationHub> hubContext,
        ILogger<NotificationService> logger)
    {
        _context = context;
        _templateService = templateService;
        _emailSender = emailSender;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<Notification> NotifierAsync(
        string code,
        string canal,
        string langue,
        int? destinataireId,
        string? email,
        string? telephone,
        IDictionary<string, string> variables)
    {
        var (sujet, messageHtml, messageTexte) = await _templateService.RenderAsync(code, canal, langue, variables);

        var notification = new Notification
        {
            DestinataireId = destinataireId,
            DestinataireEmail = email,
            DestinataireTelephone = telephone,
            Canal = canal,
            Titre = sujet,
            Message = messageTexte,
            Statut = "EnAttente",
            DateCreation = DateTime.UtcNow,
            NombreTentatives = 0
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        await ExpedierNotificationAsync(notification, messageHtml);

        await _context.SaveChangesAsync();
        return notification;
    }

    private async Task ExpedierNotificationAsync(Notification notification, string messageHtml)
    {
        notification.NombreTentatives++;

        try
        {
            switch (notification.Canal.ToUpperInvariant())
            {
                case "EMAIL":
                    if (string.IsNullOrWhiteSpace(notification.DestinataireEmail))
                    {
                        throw new InvalidOperationException("Adresse email du destinataire manquante.");
                    }
                    await _emailSender.SendEmailAsync(
                        notification.DestinataireEmail,
                        notification.Titre,
                        messageHtml);
                    break;

                case "INTERNE":
                case "SIGNALR":
                    // Envoi via SignalR Hub en temps réel
                    if (notification.DestinataireId.HasValue)
                    {
                        await _hubContext.Clients.Group($"User_{notification.DestinataireId.Value}")
                            .SendAsync("RecevoirNotification", new
                            {
                                notification.Id,
                                notification.Titre,
                                notification.Message,
                                notification.DateCreation
                            });
                    }
                    else
                    {
                        await _hubContext.Clients.All.SendAsync("RecevoirNotification", new
                        {
                            notification.Id,
                            notification.Titre,
                            notification.Message,
                            notification.DateCreation
                        });
                    }
                    break;

                case "SMS":
                    // Passerelle SMS (Twilio / simulation sécurisée)
                    _logger.LogInformation("[SMS] Envoi à {Telephone} : {Message}", notification.DestinataireTelephone, notification.Message);
                    break;

                case "PUSH":
                    // Notifications push (Firebase Cloud Messaging / simulation)
                    _logger.LogInformation("[PUSH] Envoi à l'utilisateur {UserId} : {Titre}", notification.DestinataireId, notification.Titre);
                    break;

                default:
                    throw new NotSupportedException($"Canal '{notification.Canal}' non supporté.");
            }

            notification.Statut = "Envoye";
            notification.DateEnvoi = DateTime.UtcNow;
            notification.DerniereErreur = null;
        }
        catch (Exception ex)
        {
            notification.Statut = "Echec";
            notification.DerniereErreur = ex.Message;
            _logger.LogError(ex, "Échec de l'envoi de la notification {Id} (canal: {Canal})", notification.Id, notification.Canal);
        }
    }

    public async Task NotifierVirementRecuAsync(
        string email,
        string? telephone,
        string nomClient,
        string numeroCompte,
        decimal montant,
        string motif)
    {
        var vars = new Dictionary<string, string>
        {
            { "NomClient", nomClient },
            { "NumeroCompte", numeroCompte },
            { "Montant", montant.ToString("N2") },
            { "Motif", motif ?? "Virement" }
        };

        if (!string.IsNullOrWhiteSpace(email))
        {
            await NotifierAsync("VIREMENT_RECU", "Email", "fr-BI", null, email, telephone, vars);
        }

        // Notification interne temps réel
        await _hubContext.Clients.All.SendAsync("RecevoirNotification", new
        {
            Titre = "Virement reçu",
            Message = $"Virement de {montant:N2} BIF reçu pour {nomClient} ({numeroCompte})"
        });
    }

    public async Task NotifierPretValideAsync(
        string email,
        string? telephone,
        string nomClient,
        string referencePret,
        decimal montant)
    {
        var vars = new Dictionary<string, string>
        {
            { "NomClient", nomClient },
            { "Reference", referencePret },
            { "Montant", montant.ToString("N2") }
        };

        if (!string.IsNullOrWhiteSpace(email))
        {
            await NotifierAsync("PRET_VALIDE", "Email", "fr-BI", null, email, telephone, vars);
        }
    }

    public async Task NotifierAlerteAMLCritiqueAsync(
        string numeroCompte,
        string motif,
        string niveauRisque)
    {
        var vars = new Dictionary<string, string>
        {
            { "NumeroCompte", numeroCompte },
            { "Motif", motif },
            { "NiveauRisque", niveauRisque }
        };

        // Alerte aux officiers de conformité via SignalR et Notification
        await _hubContext.Clients.Group("Role_Conformite").SendAsync("RecevoirNotification", new
        {
            Titre = $"[AML] Alerte {niveauRisque}",
            Message = $"Alerte critique sur le compte {numeroCompte} : {motif}"
        });
    }

    public async Task NotifierRappelEcheanceAsync(
        string email,
        string? telephone,
        string nomClient,
        decimal montant,
        DateTime dateEcheance)
    {
        var vars = new Dictionary<string, string>
        {
            { "NomClient", nomClient },
            { "Montant", montant.ToString("N2") },
            { "DateEcheance", dateEcheance.ToString("dd/MM/yyyy") }
        };

        if (!string.IsNullOrWhiteSpace(email))
        {
            await NotifierAsync("ECHEANCE_RAPPEL", "Email", "fr-BI", null, email, telephone, vars);
        }
        if (!string.IsNullOrWhiteSpace(telephone))
        {
            await NotifierAsync("ECHEANCE_RAPPEL", "SMS", "fr-BI", null, email, telephone, vars);
        }
    }

    public async Task<int> ReessayerEchecsAsync(int maxTentatives = 3)
    {
        var echecs = await _context.Notifications
            .Where(n => n.Statut == "Echec" && n.NombreTentatives < maxTentatives)
            .Take(50)
            .ToListAsync();

        int reussis = 0;
        foreach (var notif in echecs)
        {
            await ExpedierNotificationAsync(notif, notif.Message);
            if (notif.Statut == "Envoye")
            {
                reussis++;
            }
        }

        await _context.SaveChangesAsync();
        return reussis;
    }

    public async Task<NotificationListViewModel> GetPagedAsync(
        string? canal,
        string? statut,
        int page,
        int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = pageSize < 1 ? 15 : pageSize;

        var query = _context.Notifications
            .AsNoTracking()
            .Include(n => n.Destinataire)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(canal))
        {
            query = query.Where(n => n.Canal == canal);
        }

        if (!string.IsNullOrWhiteSpace(statut))
        {
            query = query.Where(n => n.Statut == statut);
        }

        var total = await query.CountAsync();

        var notifs = await query
            .OrderByDescending(n => n.DateCreation)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new NotificationListViewModel
        {
            Notifications = notifs,
            Canal = canal,
            Statut = statut,
            Page = page,
            PageSize = pageSize,
            TotalItems = total
        };
    }

    public async Task<NotificationDetailsViewModel?> GetDetailsAsync(int id)
    {
        var n = await _context.Notifications
            .Include(x => x.Destinataire)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (n is null) return null;

        return new NotificationDetailsViewModel
        {
            Notification = n
        };
    }
}

