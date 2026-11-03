using AkarangaBankMvc.Web.Models;

namespace AkarangaBankMvc.Web.ViewModels;

public class NotificationListViewModel
{
    public IEnumerable<Notification> Notifications { get; set; } = new List<Notification>();
    public string? Canal { get; set; }
    public string? Statut { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 15;
    public int TotalItems { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);
}

public class NotificationDetailsViewModel
{
    public Notification Notification { get; set; } = null!;
}

