using System.Data;
using System.Text;
using AkarangaBankMvc.Web.Data;
using AkarangaBankMvc.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace AkarangaBankMvc.Web.Services;

public class CNRFService : ICNRFService
{
    private readonly AkarangaBankContext _context;

    public CNRFService(AkarangaBankContext context)
    {
        _context = context;
    }

    public async Task<DeclarationSoupcon> GenererDOSAsync(
        int alerteId,
        int redigeParId)
    {
        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        await VerifierUtilisateurAsync(redigeParId);

        var alerte = await _context.AlertesAML
            .Include(a => a.Client)
            .Include(a => a.Operation)
            .FirstOrDefaultAsync(a => a.Id == alerteId)
            ?? throw new InvalidOperationException(
                "Alerte introuvable.");

        // Un clic répété ne crée pas une deuxième DOS.
        var existante = await _context.DeclarationsSoupcon
            .Where(d =>
                d.AlerteId == alerteId &&
                d.TypeDeclaration == "DOS" &&
                d.Statut != "Rejetee")
            .OrderByDescending(d => d.Id)
            .FirstOrDefaultAsync();

        if (existante is not null)
        {
            await transaction.CommitAsync();
            return existante;
        }

        var date = DateTime.UtcNow;
        var reference = GenererReference();

        var contenu = await GenererContenuDOSAsync(alerte);

        var declaration = new DeclarationSoupcon
        {
            Reference = reference,
            AlerteId = alerte.Id,
            ClientId = alerte.ClientId,
            TypeDeclaration = "DOS",
            Contenu =
                $"Référence déclaration : {reference}\n" +
                $"Date de création UTC : {date:dd/MM/yyyy HH:mm:ss}\n\n" +
                contenu,
            MotifSuspicion = alerte.Description,
            Statut = "Brouillon",
            DateCreation = date,
            RedigeParId = redigeParId
        };

        _context.DeclarationsSoupcon.Add(declaration);
        await _context.SaveChangesAsync();

        AjouterAudit(
            redigeParId,
            declaration.Id,
            "GenerationDOS",
            null,
            "Brouillon",
            $"Création pédagogique de {reference}, alerte {alerte.Id}.");

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return declaration;
    }

    public async Task<bool> ValiderAsync(
        int declarationId,
        int utilisateurId)
    {
        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        await VerifierUtilisateurAsync(utilisateurId);

        var declaration = await ChargerDeclarationAsync(declarationId);

        if (declaration is null)
        {
            return false;
        }

        if (declaration.Statut == "Validee")
        {
            await transaction.CommitAsync();
            return false;
        }

        if (declaration.Statut != "Brouillon")
        {
            throw new InvalidOperationException(
                "Seule une déclaration en brouillon peut être validée.");
        }

        if (string.IsNullOrWhiteSpace(declaration.Contenu) ||
            string.IsNullOrWhiteSpace(declaration.MotifSuspicion))
        {
            throw new InvalidOperationException(
                "Le contenu et le motif de suspicion sont obligatoires.");
        }

        declaration.Statut = "Validee";

        AjouterAudit(
            utilisateurId,
            declaration.Id,
            "ValidationDOS",
            "Brouillon",
            "Validee",
            $"Validation pédagogique de {declaration.Reference}.");

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return true;
    }

    public async Task<bool> TransmettreAsync(int declarationId)
    {
        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        var declaration = await ChargerDeclarationAsync(declarationId);

        if (declaration is null)
        {
            return false;
        }

        if (declaration.Statut is "Transmise" or "Accusee")
        {
            await transaction.CommitAsync();
            return false;
        }

        if (declaration.Statut != "Validee")
        {
            throw new InvalidOperationException(
                "La déclaration doit être validée avant la transmission simulée.");
        }

        // Aucun appel HTTP, email ou dépôt externe.
        declaration.Statut = "Transmise";
        declaration.DateTransmission = DateTime.UtcNow;

        AjouterAudit(
            null,
            declaration.Id,
            "SimulationEnvoiDOS",
            "Validee",
            "Transmise",
            $"SIMULATION LOCALE : {declaration.Reference}. " +
            "Aucun envoi réel à la CNRF.");

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return true;
    }

    public async Task<bool> EnregistrerAccuseAsync(
        int declarationId,
        string reponse)
    {
        reponse = reponse?.Trim() ?? string.Empty;

        const string prefixe = "[SIMULATION] ";

        if (reponse.Length == 0 ||
            reponse.Length + prefixe.Length > 1000)
        {
            throw new InvalidOperationException(
                "La réponse est obligatoire et doit tenir dans 1000 " +
                "caractères avec le préfixe de simulation.");
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        var declaration = await ChargerDeclarationAsync(declarationId);

        if (declaration is null)
        {
            return false;
        }

        if (declaration.Statut == "Accusee")
        {
            await transaction.CommitAsync();
            return false;
        }

        if (declaration.Statut != "Transmise")
        {
            throw new InvalidOperationException(
                "Seule une déclaration transmise en simulation " +
                "peut recevoir un accusé simulé.");
        }

        declaration.Statut = "Accusee";
        declaration.DateAccuseReception = DateTime.UtcNow;
        declaration.ReponseCNRF = prefixe + reponse;

        AjouterAudit(
            null,
            declaration.Id,
            "SimulationAccuseDOS",
            "Transmise",
            "Accusee",
            $"Accusé simulé pour {declaration.Reference}. " +
            "Aucune réception réelle confirmée par la CNRF.");

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return true;
    }

    public async Task<IEnumerable<DeclarationSoupcon>>
        GetDeclarationsAsync(string? statut)
    {
        var query = _context.DeclarationsSoupcon
            .AsNoTracking()
            .Include(d => d.Client)
            .Include(d => d.Alerte)
            .Include(d => d.RedigePar)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(statut))
        {
            var filtre = statut.Trim();
            query = query.Where(d => d.Statut == filtre);
        }

        return await query
            .OrderByDescending(d => d.DateCreation)
            .ThenByDescending(d => d.Id)
            .ToListAsync();
    }

    public Task<string> GenererContenuDOSAsync(AlerteAML alerte)
    {
        ArgumentNullException.ThrowIfNull(alerte);

        if (alerte.Client is null)
        {
            throw new InvalidOperationException(
                "Les informations du client doivent être chargées.");
        }

        var sb = new StringBuilder();

        sb.AppendLine("DÉCLARATION D'OPÉRATION SUSPECTE — DOS");
        sb.AppendLine("Akaranga Bank — exercice pédagogique");
        sb.AppendLine("DOCUMENT DE TEST — NON TRANSMIS À UNE AUTORITÉ");
        sb.AppendLine();

        sb.AppendLine("--- IDENTIFICATION DU CLIENT ---");
        sb.AppendLine(
            $"Nom : {alerte.Client.Nom} {alerte.Client.Prenom}");
        sb.AppendLine(
            $"Numéro client : {alerte.Client.NumeroClient}");
        sb.AppendLine($"CNI : {alerte.Client.NumeroCNI}");
        sb.AppendLine($"Téléphone : {alerte.Client.Telephone}");
        sb.AppendLine();

        sb.AppendLine("--- OPÉRATION CONCERNÉE ---");

        if (alerte.Operation is not null)
        {
            var operation = alerte.Operation;

            sb.AppendLine($"Référence : {operation.Reference}");
            sb.AppendLine($"Type : {operation.TypeOperation}");
            sb.AppendLine(
                $"Montant : {operation.Montant:N2} {operation.Devise}");
            sb.AppendLine(
                $"Date UTC : {operation.DateOperation:dd/MM/yyyy HH:mm:ss}");
        }
        else
        {
            sb.AppendLine(
                "Aucune opération unique associée à cette alerte.");
        }

        sb.AppendLine();
        sb.AppendLine("--- MOTIF DE SUSPICION ---");
        sb.AppendLine($"Alerte ID : {alerte.Id}");
        sb.AppendLine($"Type : {alerte.TypeAlerte}");
        sb.AppendLine($"Niveau : {alerte.Niveau}");
        sb.AppendLine($"Description : {alerte.Description}");
        sb.AppendLine();

        sb.AppendLine("--- DESTINATION PRÉVUE DANS L'EXERCICE ---");
        sb.AppendLine("CNRF — aucune transmission réelle.");
        sb.AppendLine();

        sb.AppendLine("--- LIMITES ---");
        sb.AppendLine(
            "Ce modèle pédagogique n'est pas un formulaire officiel.");
        sb.AppendLine(
            "Les mentions légales, le format et le canal de transmission " +
            "doivent être vérifiés avant toute utilisation réelle.");

        return Task.FromResult(sb.ToString());
    }

    private async Task<DeclarationSoupcon?> ChargerDeclarationAsync(int id)
    {
        var declaration = await _context.DeclarationsSoupcon
            .FirstOrDefaultAsync(d => d.Id == id);

        if (declaration is not null)
        {
            await _context.Entry(declaration).ReloadAsync();
        }

        return declaration;
    }

    private async Task VerifierUtilisateurAsync(int id)
    {
        if (id <= 0 || !await _context.Utilisateurs
            .AnyAsync(u => u.Id == id && u.EstActif))
        {
            throw new InvalidOperationException(
                "Utilisateur inexistant ou inactif.");
        }
    }

    private void AjouterAudit(
        int? utilisateurId,
        int declarationId,
        string action,
        string? avant,
        string apres,
        string details)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            DateEvenement = DateTime.UtcNow,
            UtilisateurId = utilisateurId,
            Action = action,
            TableConcernee = "DeclarationsSoupcon",
            EnregistrementId = declarationId,
            ValeurAvant = avant,
            ValeurApres = apres,
            Details = details
        });
    }

    private static string GenererReference()
    {
        return $"DOS-{DateTime.UtcNow:yyyyMMdd}-" +
            Guid.NewGuid().ToString("N")[..16].ToUpperInvariant();
    }
}