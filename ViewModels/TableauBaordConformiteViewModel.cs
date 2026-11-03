using AkarangaBankMvc.Web.Models;

namespace AkarangaBankMvc.Web.ViewModels;

public class TableauBordConformiteViewModel
{
    public int TotalAlertes { get; set; }

    public int AlertesOuvertes { get; set; }
    public int AlertesEnCours { get; set; }
    public int AlertesTraitees { get; set; }
    public int AlertesClassees { get; set; }

    public int AlertesFaibles { get; set; }
    public int AlertesMoyennes { get; set; }
    public int AlertesElevees { get; set; }
    public int AlertesCritiques { get; set; }

    public decimal SeuilVigilance { get; set; }
    public decimal SeuilDeclarationDepot { get; set; }
    public decimal SeuilDeclarationRetrait { get; set; }
    public decimal SeuilBlocage { get; set; }

    public int DeclarationsBrouillon { get; set; }
    public int DeclarationsValidees { get; set; }
    public int DeclarationsTransmises { get; set; }
    public int DeclarationsAccusees { get; set; }

    public List<AlerteAML> AlertesRecentes { get; set; } = new();
}