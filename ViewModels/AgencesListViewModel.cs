// namespace AkarangaBankMvc.Web.ViewModels;

// public class AgenceListViewModel
// {
//     public IEnumerable<AgenceListItemViewModel> Agences { get; set; } =
//         new List<AgenceListItemViewModel>();

//     public int TotalAgences => Agences.Count();
//     public int TotalClients => Agences.Sum(a => a.NombreClients);
// }

// public class AgenceListItemViewModel
// {
//     public int Id { get; set; }
//     public string Code { get; set; } = string.Empty;
//     public string Nom { get; set; } = string.Empty;
//     public string Ville { get; set; } = string.Empty;
//     public bool EstSiege { get; set; }
//     public int NombreClients { get; set; }
// }

// Chap 5
using AkarangaBankMvc.Web.Models;

namespace AkarangaBankMvc.Web.ViewModels;

// ViewModel utilisé par la page Index des agences.
// Il contient la liste paginée + les paramètres de recherche.
public class AgenceListViewModel
{
    public IEnumerable<Agence> Agences { get; set; } = new List<Agence>();
    public string? Recherche { get; set; }
    public int Page { get; set; } = 1;
    public int TaillePage { get; set; } = 10;
    public int Total { get; set; }

    // Propriété calculée : nombre total de pages.
    public int TotalPages => (int)Math.Ceiling((double)Total / TaillePage);
}