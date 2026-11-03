using AkarangaBankMvc.Web.Models;
using AkarangaBankMvc.Web.ViewModels;

namespace AkarangaBankMvc.Web.Services;

public interface IDocumentService
{
    Task<Document> UploadAsync(DocumentUploadViewModel model, int? userId, string? ipAddress);
    Task<(Document Document, Stream Stream, string ContentType)?> TelechargerAsync(int id, int? userId, string? ipAddress);
    Task<DocumentDetailsViewModel?> GetDetailsAsync(int id, int? userId, string? ipAddress);
    Task<bool> ArchiverAsync(int id, int? userId, string? ipAddress);
    Task<bool> SupprimerAsync(int id, int? userId, string? ipAddress);
    Task<int> PurgerAsync(int joursConservation = 30);
    Task<DocumentListViewModel> GetPagedAsync(string? recherche, string? categorie, string? statut, int? clientId, int page, int pageSize);
}

