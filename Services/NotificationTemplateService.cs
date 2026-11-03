using System.Text.RegularExpressions;
using AkarangaBankMvc.Web.Data;
using AkarangaBankMvc.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace AkarangaBankMvc.Web.Services;

public class NotificationTemplateService : INotificationTemplateService
{
    private readonly AkarangaBankContext _context;

    public NotificationTemplateService(AkarangaBankContext context)
    {
        _context = context;
    }

    public async Task<NotificationTemplate?> GetTemplateAsync(string code, string canal, string langue)
    {
        // 1. Recherche exacte
        var template = await _context.NotificationTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Code == code && t.Canal == canal && t.Langue == langue);

        if (template is not null) return template;

        // 2. Repli sur le français
        if (langue != "fr-BI")
        {
            template = await _context.NotificationTemplates
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Code == code && t.Canal == canal && t.Langue == "fr-BI");
        }

        return template;
    }

    public async Task<(string Sujet, string MessageHtml, string MessageTexte)> RenderAsync(
        string code,
        string canal,
        string langue,
        IDictionary<string, string> variables)
    {
        var template = await GetTemplateAsync(code, canal, langue);

        string sujet;
        string html;
        string texte;

        if (template is not null)
        {
            sujet = template.Sujet;
            html = template.ContenuHtml;
            texte = template.ContenuTexte;
        }
        else
        {
            // Modèles par défaut si non enregistrés en base
            (sujet, html, texte) = GetDefaultTemplate(code, canal);
        }

        foreach (var (k, v) in variables)
        {
            var pattern = @"\{\{\s*" + Regex.Escape(k) + @"\s*\}\}";
            sujet = Regex.Replace(sujet, pattern, v ?? string.Empty);
            html = Regex.Replace(html, pattern, v ?? string.Empty);
            texte = Regex.Replace(texte, pattern, v ?? string.Empty);
        }

        return (sujet, html, texte);
    }

    private static (string Sujet, string Html, string Texte) GetDefaultTemplate(string code, string canal)
    {
        return code.ToUpperInvariant() switch
        {
            "VIREMENT_RECU" => (
                "Akaranga Bank - Virement reçu",
                "<p>Bonjour <strong>{{NomClient}}</strong>,</p><p>Votre compte <strong>{{NumeroCompte}}</strong> a été crédité d'un virement de <strong>{{Montant}} BIF</strong>.</p><p>Motif : {{Motif}}</p>",
                "Bonjour {{NomClient}}, votre compte {{NumeroCompte}} a été crédité de {{Montant}} BIF. Motif : {{Motif}}"
            ),
            "PRET_VALIDE" => (
                "Akaranga Bank - Validation de votre demande de prêt",
                "<p>Bonjour <strong>{{NomClient}}</strong>,</p><p>Votre prêt référence <strong>{{Reference}}</strong> d'un montant de <strong>{{Montant}} BIF</strong> a été validé.</p>",
                "Bonjour {{NomClient}}, votre prêt {{Reference}} de {{Montant}} BIF a été validé avec succès."
            ),
            "ALERTE_AML" => (
                "Akaranga Bank [CONFORMITE] - Alerte LBC/FT critique",
                "<p>Alerte AML générée pour le compte <strong>{{NumeroCompte}}</strong>.</p><p>Niveau de risque : <strong>{{NiveauRisque}}</strong></p><p>Motif : {{Motif}}</p>",
                "ALERTE AML critique pour le compte {{NumeroCompte}} (Risque: {{NiveauRisque}}). Motif : {{Motif}}"
            ),
            "ECHEANCE_RAPPEL" => (
                "Akaranga Bank - Rappel d'échéance de prêt",
                "<p>Bonjour <strong>{{NomClient}}</strong>,</p><p>Nous vous rappelons que l'échéance de votre prêt arrive à terme le <strong>{{DateEcheance}}</strong> pour un montant de <strong>{{Montant}} BIF</strong>.</p>",
                "Rappel Akaranga Bank: Échéance de prêt le {{DateEcheance}} pour {{Montant}} BIF."
            ),
            _ => (
                "Akaranga Bank - Notification",
                "<p>{{Message}}</p>",
                "{{Message}}"
            )
        };
    }
}

