using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AkarangaBankMvc.Web.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        // Associer l'utilisateur connecté à son groupe personnel par UserId
        if (Context.UserIdentifier is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{Context.UserIdentifier}");
        }

        // Associer aux groupes de rôles
        if (Context.User is not null)
        {
            if (Context.User.IsInRole("admin"))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "Role_Admin");
            }
            if (Context.User.IsInRole("conformite"))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "Role_Conformite");
            }
            if (Context.User.IsInRole("guichetier"))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "Role_Guichetier");
            }
        }

        await base.OnConnectedAsync();
    }
}

