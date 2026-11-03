using System.Security.Cryptography;
using AkarangaBankMvc.Web.Data;
using AkarangaBankMvc.Web.Models;
using AkarangaBankMvc.Web.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AkarangaBankMvc.Web.Services;

public class DocumentService : IDocumentService
{
    private static readonly HashSet<string> ExtensionsAutorisees = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".jpg", ".jpeg", ".png", ".docx", ".xlsx"
    };

    private const long TailleMaximaleOctets = 10 * 1024 * 1024; // 10 Mo

    private readonly AkarangaBankContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<DocumentService> _logger;
    private readonly string _storageDirectory;

    public DocumentService(
        AkarangaBankContext context,
        IWebHostEnvironment environment,
        ILogger<DocumentService> logger)
    {
        _context = context;
        _environment = environment;
        _logger = logger;

        // Stockage hors du wwwroot pour raisons de sécurité bancaire (ENF-14.1)
        _storageDirectory = Path.Combine(_environment.ContentRootPath, "App_Data", "Documents");
        if (!Directory.Exists(_storageDirectory))
        {
            Directory.CreateDirectory(_storageDirectory);
        }
    }

    public async Task<Document> UploadAsync(
        DocumentUploadViewModel model,
        int? userId,
        string? ipAddress)
    {
        if (model.Fichier is null || model.Fichier.Length == 0)
        {
            throw new ArgumentException("Le fichier est vide ou absent.");
        }

        if (model.Fichier.Length > TailleMaximaleOctets)
        {
            throw new InvalidOperationException("La taille du fichier dépasse la limite autorisée de 10 Mo.");
        }

        var ext = Path.GetExtension(model.Fichier.FileName);
        if (string.IsNullOrEmpty(ext) || !ExtensionsAutorisees.Contains(ext))
        {
            throw new InvalidOperationException($"L'extension '{ext}' n'est pas autorisée. Formats acceptés : PDF, JPG, PNG, DOCX, XLSX.");
        }

        // Calcul du hash SHA-256 (EF-14.2) et sauvegarde sur disque avec nom unique GUID (EF-14.3)
        var nomFichierStocke = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
        var cheminComplet = Path.Combine(_storageDirectory, nomFichierStocke);
        string sha256Hex;

        using (var stream = model.Fichier.OpenReadStream())
        {
            var hashBytes = await SHA256.HashDataAsync(stream);
            sha256Hex = Convert.ToHexString(hashBytes).ToLowerInvariant();

            stream.Position = 0;
            using var fileOutput = File.Create(cheminComplet);
            await stream.CopyToAsync(fileOutput);
        }

        var document = new Document
        {
            Titre = model.Titre.Trim(),
            NomFichierOriginal = Path.GetFileName(model.Fichier.FileName),
            NomFichierStocke = nomFichierStocke,
            CheminRelatif = Path.Combine("App_Data", "Documents", nomFichierStocke),
            ContentType = string.IsNullOrWhiteSpace(model.Fichier.ContentType) ? "application/octet-stream" : model.Fichier.ContentType,
            Extension = ext.ToLowerInvariant(),
            TailleOctets = model.Fichier.Length,
            Sha256Hash = sha256Hex,
            Categorie = string.IsNullOrWhiteSpace(model.Categorie) ? "KYC" : model.Categorie,
            Statut = "Actif",
            ClientId = model.ClientId,
            CreeParUtilisateurId = userId,
            DateCreation = DateTime.UtcNow
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        // Tracer l'accès de création (EF-14.4)
        _context.DocumentsAcces.Add(new DocumentAcces
        {
            DocumentId = document.Id,
            UtilisateurId = userId,
            TypeAcces = "Creation",
            DateAcces = DateTime.UtcNow,
            AdresseIp = ipAddress
        });
        await _context.SaveChangesAsync();

        _logger.LogInformation("Document {Id} ({Titre}) enregistré avec succès par l'utilisateur {UserId}", document.Id, document.Titre, userId);

        return document;
    }

    public async Task<(Document Document, Stream Stream, string ContentType)?> TelechargerAsync(
        int id,
        int? userId,
        string? ipAddress)
    {
        var doc = await _context.Documents
            .FirstOrDefaultAsync(d => d.Id == id && d.Statut != "Supprime");

        if (doc is null) return null;

        var cheminComplet = Path.Combine(_environment.ContentRootPath, doc.CheminRelatif);
        if (!File.Exists(cheminComplet))
        {
            _logger.LogWarning("Fichier physique introuvable pour le document {Id} : {Chemin}", id, cheminComplet);
            return null;
        }

        // Tracer l'accès (EF-14.4 / ENF-14.2)
        _context.DocumentsAcces.Add(new DocumentAcces
        {
            DocumentId = doc.Id,
            UtilisateurId = userId,
            TypeAcces = "Telechargement",
            DateAcces = DateTime.UtcNow,
            AdresseIp = ipAddress
        });
        await _context.SaveChangesAsync();

        var stream = new FileStream(cheminComplet, FileMode.Open, FileAccess.Read, FileShare.Read);
        return (doc, stream, doc.ContentType);
    }

    public async Task<DocumentDetailsViewModel?> GetDetailsAsync(
        int id,
        int? userId,
        string? ipAddress)
    {
        var doc = await _context.Documents
            .Include(d => d.Client)
            .Include(d => d.CreeParUtilisateur)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (doc is null) return null;

        // Tracer la consultation
        _context.DocumentsAcces.Add(new DocumentAcces
        {
            DocumentId = doc.Id,
            UtilisateurId = userId,
            TypeAcces = "Consultation",
            DateAcces = DateTime.UtcNow,
            AdresseIp = ipAddress
        });
        await _context.SaveChangesAsync();

        var acces = await _context.DocumentsAcces
            .Include(a => a.Utilisateur)
            .Where(a => a.DocumentId == id)
            .OrderByDescending(a => a.DateAcces)
            .Take(50)
            .ToListAsync();

        return new DocumentDetailsViewModel
        {
            Document = doc,
            HistoriqueAcces = acces
        };
    }

    public async Task<bool> ArchiverAsync(int id, int? userId, string? ipAddress)
    {
        var doc = await _context.Documents.FindAsync(id);
        if (doc is null || doc.Statut == "Supprime") return false;

        doc.Statut = "Archive";

        _context.DocumentsAcces.Add(new DocumentAcces
        {
            DocumentId = doc.Id,
            UtilisateurId = userId,
            TypeAcces = "Archivage",
            DateAcces = DateTime.UtcNow,
            AdresseIp = ipAddress
        });

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SupprimerAsync(int id, int? userId, string? ipAddress)
    {
        var doc = await _context.Documents.FindAsync(id);
        if (doc is null) return false;

        doc.Statut = "Supprime";
        doc.DateSuppression = DateTime.UtcNow;

        _context.DocumentsAcces.Add(new DocumentAcces
        {
            DocumentId = doc.Id,
            UtilisateurId = userId,
            TypeAcces = "Suppression",
            DateAcces = DateTime.UtcNow,
            AdresseIp = ipAddress
        });

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<int> PurgerAsync(int joursConservation = 30)
    {
        var dateLimite = DateTime.UtcNow.AddDays(-joursConservation);

        var aPurger = await _context.Documents
            .Where(d => d.Statut == "Supprime" && d.DateSuppression.HasValue && d.DateSuppression.Value <= dateLimite)
            .ToListAsync();

        int nbPurges = 0;
        foreach (var doc in aPurger)
        {
            var chemin = Path.Combine(_environment.ContentRootPath, doc.CheminRelatif);
            if (File.Exists(chemin))
            {
                try
                {
                    File.Delete(chemin);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Erreur lors de la suppression physique du fichier {Chemin}", chemin);
                }
            }
            _context.Documents.Remove(doc);
            nbPurges++;
        }

        if (nbPurges > 0)
        {
            await _context.SaveChangesAsync();
        }

        return nbPurges;
    }

    public async Task<DocumentListViewModel> GetPagedAsync(
        string? recherche,
        string? categorie,
        string? statut,
        int? clientId,
        int page,
        int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = pageSize < 1 ? 10 : pageSize;

        var query = _context.Documents
            .AsNoTracking()
            .Include(d => d.Client)
            .Include(d => d.CreeParUtilisateur)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(recherche))
        {
            var r = recherche.Trim();
            query = query.Where(d =>
                d.Titre.Contains(r) ||
                d.NomFichierOriginal.Contains(r) ||
                (d.Client != null && (d.Client.Nom.Contains(r) || d.Client.Prenom.Contains(r) || d.Client.NumeroClient.Contains(r))));
        }

        if (!string.IsNullOrWhiteSpace(categorie))
        {
            query = query.Where(d => d.Categorie == categorie);
        }

        if (!string.IsNullOrWhiteSpace(statut))
        {
            query = query.Where(d => d.Statut == statut);
        }
        else
        {
            // Par défaut masquer les supprimés
            query = query.Where(d => d.Statut != "Supprime");
        }

        if (clientId.HasValue)
        {
            query = query.Where(d => d.ClientId == clientId.Value);
        }

        var total = await query.CountAsync();

        var docs = await query
            .OrderByDescending(d => d.DateCreation)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new DocumentListViewModel
        {
            Documents = docs,
            Recherche = recherche,
            Categorie = categorie,
            Statut = statut,
            ClientId = clientId,
            Page = page,
            PageSize = pageSize,
            TotalItems = total
        };
    }
}

