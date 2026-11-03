using System.ComponentModel.DataAnnotations;
using AkarangaBankMvc.Web.Models;

namespace AkarangaBankMvc.Web.ViewModels;

public class JournalViewModel
{
    public IEnumerable<Operation> Operations { get; set; }
        = Enumerable.Empty<Operation>();

    [Display(Name = "Du")]
    public DateTime? Du { get; set; }

    [Display(Name = "Au")]
    public DateTime? Au { get; set; }

    [Display(Name = "Type d’opération")]
    public string? TypeOperation { get; set; }

    public int Page { get; set; } = 1;

    public int TaillePage { get; set; } = 20;

    public int Total { get; set; }

    public int TotalPages =>
        TaillePage <= 0
            ? 0
            : (int)Math.Ceiling((double)Total / TaillePage);

    public bool APagePrecedente => Page > 1;

    public bool APageSuivante => Page < TotalPages;
}