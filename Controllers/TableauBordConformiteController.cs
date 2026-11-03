using AkarangaBankMvc.Web.Data;
using AkarangaBankMvc.Web.Services;
using AkarangaBankMvc.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AkarangaBankMvc.Web.Controllers;

[Authorize(Policy = "AdminOnly")]
public class TableauBordConformiteController : Controller
{
    private readonly AkarangaBankContext _context;
    private readonly ISeuilAMLService _seuils;

    public TableauBordConformiteController(
        AkarangaBankContext context,
        ISeuilAMLService seuils)
    {
        _context = context;
        _seuils = seuils;
    }

    public async Task<IActionResult> Index()
    {
        var alertes = await _context.AlertesAML
            .AsNoTracking()
            .ToListAsync();

        var declarations = await _context.DeclarationsSoupcon
            .AsNoTracking()
            .ToListAsync();

        var vm = new TableauBordConformiteViewModel
        {
            TotalAlertes = alertes.Count,

            AlertesOuvertes = alertes.Count(a => a.Statut == "Ouverte"),
            AlertesEnCours = alertes.Count(a => a.Statut == "EnCours"),
            AlertesTraitees = alertes.Count(a => a.Statut == "Traitee"),
            AlertesClassees = alertes.Count(a => a.Statut == "Classee"),

            AlertesFaibles = alertes.Count(a => a.Niveau == "Faible"),
            AlertesMoyennes = alertes.Count(a => a.Niveau == "Moyen"),
            AlertesElevees = alertes.Count(a => a.Niveau == "Eleve"),
            AlertesCritiques = alertes.Count(a => a.Niveau == "Critique"),

            DeclarationsBrouillon = declarations.Count(d => d.Statut == "Brouillon"),
            DeclarationsValidees = declarations.Count(d => d.Statut == "Validee"),
            DeclarationsTransmises = declarations.Count(d => d.Statut == "Transmise"),
            DeclarationsAccusees = declarations.Count(d => d.Statut == "Accusee"),

            SeuilVigilance = await _seuils.GetSeuilVigilanceAsync(),
            SeuilDeclarationDepot = await _seuils.GetSeuilDeclarationAsync(),
            SeuilDeclarationRetrait = await _seuils.GetSeuilDeclarationAsync(),
            SeuilBlocage = await _seuils.GetSeuilBlocageAsync(),

            AlertesRecentes = await _context.AlertesAML
                .AsNoTracking()
                .Include(a => a.Client)
                .Include(a => a.Operation)
                .OrderByDescending(a => a.DateAlerte)
                .ThenByDescending(a => a.Id)
                .Take(10)
                .ToListAsync()
        };

        return View(vm);
    }
}