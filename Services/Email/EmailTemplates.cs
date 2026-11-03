using System.Net;

namespace AkarangaBankMvc.Web.Services.Email;

/// <summary>
/// Modèles HTML des emails transactionnels.
/// </summary>
public static class EmailTemplates
{
    public static string VerificationCodeEmail(string? nomComplet, string code, string action)
    {
        var nom = WebUtility.HtmlEncode(
            string.IsNullOrWhiteSpace(nomComplet) ? "cher utilisateur" : nomComplet);

        var codeHtml = WebUtility.HtmlEncode(code);

        return $"""
            <div style="font-family:Segoe UI,Arial,sans-serif;background:#f4f6f9;padding:32px 16px;">
              <div style="max-width:560px;margin:0 auto;background:#ffffff;border:1px solid #e5e7eb;border-radius:14px;overflow:hidden;">
                <div style="background:#0b3d66;color:#ffffff;padding:22px 28px;">
                  <div style="font-size:20px;font-weight:700;">Akaranga Bank</div>
                  <div style="font-size:12px;opacity:.8;">Sécurité du compte</div>
                </div>
                <div style="padding:28px;color:#1f2937;font-size:14px;line-height:1.7;">
                  <p>Bonjour {nom},</p>
                  <p>Vous avez demandé à {WebUtility.HtmlEncode(action)}. Saisissez le code de vérification ci-dessous :</p>
                  <div style="margin:24px 0;text-align:center;">
                    <span style="display:inline-block;letter-spacing:10px;font-size:32px;font-weight:700;color:#0b3d66;background:#f1f5f9;border:1px dashed #94a3b8;border-radius:12px;padding:14px 22px;">{codeHtml}</span>
                  </div>
                  <p>Ce code est valable <strong>10 minutes</strong> et ne peut être utilisé qu'une seule fois.</p>
                  <p>Si vous n'êtes pas à l'origine de cette demande, ignorez cet email : aucun changement ne sera effectué.</p>
                  <p style="margin-bottom:0;">Cordialement,<br />L'équipe Akaranga Bank</p>
                </div>
                <div style="background:#f8fafc;border-top:1px solid #e5e7eb;padding:14px 28px;color:#6b7280;font-size:11px;">
                  Ne communiquez jamais ce code à une tierce personne.
                </div>
              </div>
            </div>
            """;
    }

    public static string ConfirmationEmail(string? nomComplet, string confirmationLink)
    {
        var nom = WebUtility.HtmlEncode(
            string.IsNullOrWhiteSpace(nomComplet) ? "cher utilisateur" : nomComplet);

        var lien = WebUtility.HtmlEncode(confirmationLink);

        return $"""
            <div style="font-family:Segoe UI,Arial,sans-serif;background:#f4f6f9;padding:32px 16px;">
              <div style="max-width:560px;margin:0 auto;background:#ffffff;border:1px solid #e5e7eb;border-radius:14px;overflow:hidden;">
                <div style="background:#0b3d66;color:#ffffff;padding:22px 28px;">
                  <div style="font-size:20px;font-weight:700;">Akaranga Bank</div>
                  <div style="font-size:12px;opacity:.8;">Vérification de l'adresse email</div>
                </div>
                <div style="padding:28px;color:#1f2937;font-size:14px;line-height:1.7;">
                  <p>Bonjour {nom},</p>
                  <p>Merci de confirmer votre adresse email en cliquant sur le bouton ci-dessous :</p>
                  <p style="text-align:center;margin:26px 0;">
                    <a href="{lien}" style="display:inline-block;background:#0b3d66;color:#ffffff;text-decoration:none;padding:13px 26px;border-radius:8px;font-weight:600;">Confirmer mon adresse email</a>
                  </p>
                  <p>Ce lien est valable 24 heures. Après confirmation, vous pourrez vous reconnecter à votre espace.</p>
                  <p>Si vous n'êtes pas à l'origine de cette demande, ignorez cet email.</p>
                  <p style="margin-bottom:0;">Cordialement,<br />L'équipe Akaranga Bank</p>
                </div>
                <div style="background:#f8fafc;border-top:1px solid #e5e7eb;padding:14px 28px;color:#6b7280;font-size:11px;">
                  Si le bouton ne fonctionne pas, copiez ce lien dans votre navigateur :<br />
                  <span style="word-break:break-all;">{lien}</span>
                </div>
              </div>
            </div>
            """;
    }

    public static string ResetLinkEmail(string? nomComplet, string resetLink)
    {
        var nom = WebUtility.HtmlEncode(
            string.IsNullOrWhiteSpace(nomComplet) ? "cher utilisateur" : nomComplet);

        var lien = WebUtility.HtmlEncode(resetLink);

        return $"""
            <div style="font-family:Segoe UI,Arial,sans-serif;background:#f4f6f9;padding:32px 16px;">
              <div style="max-width:560px;margin:0 auto;background:#ffffff;border:1px solid #e5e7eb;border-radius:14px;overflow:hidden;">
                <div style="background:#0b3d66;color:#ffffff;padding:22px 28px;">
                  <div style="font-size:20px;font-weight:700;">Akaranga Bank</div>
                  <div style="font-size:12px;opacity:.8;">Réinitialisation du mot de passe</div>
                </div>
                <div style="padding:28px;color:#1f2937;font-size:14px;line-height:1.7;">
                  <p>Bonjour {nom},</p>
                  <p>Utilisez le lien ci-dessous pour définir un nouveau mot de passe :</p>
                  <p style="text-align:center;margin:26px 0;">
                    <a href="{lien}" style="display:inline-block;background:#0b3d66;color:#ffffff;text-decoration:none;padding:13px 26px;border-radius:8px;font-weight:600;">Réinitialiser mon mot de passe</a>
                  </p>
                  <p>Ce lien est valable 24 heures.</p>
                  <p style="margin-bottom:0;">Cordialement,<br />L'équipe Akaranga Bank</p>
                </div>
              </div>
            </div>
            """;
    }
}
