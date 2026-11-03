using System.ComponentModel.DataAnnotations;
using AkarangaBankMvc.Web.Models;
using Microsoft.AspNetCore.Http;

namespace AkarangaBankMvc.Web.ViewModels;

public class DocumentUploadViewModel
{
    [Required(ErrorMessage = "Le titre est obligatoire.")]
    [StringLength(150)]
    public string Titre { get; set; } = string.Empty;

    [Required(ErrorMessage = "La catégorie est obligatoire.")]
    public string Categorie { get; set; } = "KYC";

    public int? ClientId { get; set; }

    [Required(ErrorMessage = "Veuillez sélectionner un fichier.")]
    public IFormFile? Fichier { get; set; }
}

public class DocumentListViewModel
{
    public IEnumerable<Document> Documents { get; set; } = new List<Document>();
    public string? Recherche { get; set; }
    public string? Categorie { get; set; }
    public string? Statut { get; set; }
    public int? ClientId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalItems { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);
}

public class DocumentDetailsViewModel
{
    public Document Document { get; set; } = null!;
    public IEnumerable<DocumentAcces> HistoriqueAcces { get; set; } = new List<DocumentAcces>();
}

