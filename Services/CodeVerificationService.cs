using System.Security.Cryptography;
using AkarangaBankMvc.Web.Data;
using AkarangaBankMvc.Web.Models;
using AkarangaBankMvc.Web.Services.Email;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace AkarangaBankMvc.Web.Services;

/// <summary>
/// Gère les codes de vérification à 6 chiffres (mot de passe oublié et
/// changement de mot de passe depuis "Mon compte").
/// </summary>
public class CodeVerificationService : ICodeVerificationService
{
    public const int DureeValiditeMinutes = 10;
    public const int NombreTentativesMaximum = 5;

    private readonly AkarangaBankContext _context;
    private readonly IAppEmailSender _emailSender;
    private readonly IDataProtector _protector;
    private readonly ILogger<CodeVerificationService> _logger;

    public CodeVerificationService(
        AkarangaBankContext context,
        IAppEmailSender emailSender,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<CodeVerificationService> logger)
    {
        _context = context;
        _emailSender = emailSender;
        _protector = dataProtectionProvider.CreateProtector("AkarangaBank.CodeVerification.v1");
        _logger = logger;
    }

    public async Task EnvoyerCodeAsync(
        ApplicationUser utilisateur,
        ObjectifCode objectif,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(utilisateur.Email))
        {
            throw new InvalidOperationException(
                "Aucune adresse email n'est associée à ce compte.");
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

        var action = objectif == ObjectifCode.ChangementMotDePasse
            ? "changer votre mot de passe"
            : "réinitialiser votre mot de passe";

        var html = EmailTemplates.VerificationCodeEmail(
            utilisateur.NomComplet,
            code,
            action);

        // On envoie d'abord : si SMTP échoue, l'ancien code reste valable.
        await _emailSender.SendEmailAsync(
            utilisateur.Email,
            "Code de vérification — Akaranga Bank",
            html,
            cancellationToken);

        var maintenant = DateTime.UtcNow;

        var anciensCodes = await _context.CodesVerification
            .Where(c => c.UtilisateurId == utilisateur.Id
                && c.Objectif == objectif
                && c.UtiliseLe == null)
            .ToListAsync(cancellationToken);

        foreach (var ancien in anciensCodes)
        {
            ancien.UtiliseLe = maintenant;
        }

        _context.CodesVerification.Add(new CodeVerification
        {
            UtilisateurId = utilisateur.Id,
            CodeProtege = _protector.Protect(code),
            Objectif = objectif,
            CreeLe = maintenant,
            ExpireLe = maintenant.AddMinutes(DureeValiditeMinutes)
        });

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Code de vérification ({Objectif}) envoyé à l'utilisateur {UtilisateurId}.",
            objectif,
            utilisateur.Id);
    }

    public async Task<ResultatVerificationCode> VerifierCodeAsync(
        ApplicationUser utilisateur,
        ObjectifCode objectif,
        string code,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return ResultatVerificationCode.CodeInvalide;
        }

        var codeNormalise = new string(code.Where(char.IsDigit).ToArray());

        if (codeNormalise.Length != 6)
        {
            return ResultatVerificationCode.CodeInvalide;
        }

        var entree = await _context.CodesVerification
            .Where(c => c.UtilisateurId == utilisateur.Id
                && c.Objectif == objectif
                && c.UtiliseLe == null)
            .OrderByDescending(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (entree is null)
        {
            return ResultatVerificationCode.AucunCode;
        }

        if (entree.Tentatives >= NombreTentativesMaximum)
        {
            return ResultatVerificationCode.TropDeTentatives;
        }

        if (entree.ExpireLe <= DateTime.UtcNow)
        {
            entree.UtiliseLe = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return ResultatVerificationCode.Expire;
        }

        string codeAttendu;

        try
        {
            codeAttendu = _protector.Unprotect(entree.CodeProtege);
        }
        catch (CryptographicException)
        {
            _logger.LogWarning(
                "Code de vérification illisible pour l'utilisateur {UtilisateurId}.",
                utilisateur.Id);

            entree.UtiliseLe = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return ResultatVerificationCode.CodeInvalide;
        }

        var correspond = CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(codeAttendu),
            System.Text.Encoding.UTF8.GetBytes(codeNormalise));

        if (!correspond)
        {
            entree.Tentatives++;

            if (entree.Tentatives >= NombreTentativesMaximum)
            {
                entree.UtiliseLe = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(cancellationToken);
            return ResultatVerificationCode.CodeInvalide;
        }

        entree.UtiliseLe = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return ResultatVerificationCode.Reussi;
    }
}
