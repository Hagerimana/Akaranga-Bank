using System.Text.Json;
using AkarangaBankMvc.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AkarangaBankMvc.Web.Data.Interceptors;

public class AuditInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor _accessor;

    public AuditInterceptor(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (context is null) return result;

        var auditEntries = new List<AuditLog>();
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is AuditLog) continue;

            if (entry.State == EntityState.Added ||
                entry.State == EntityState.Modified ||
                entry.State == EntityState.Deleted)
            {
                auditEntries.Add(CreateAuditEntry(entry));
            }
        }

        if (auditEntries.Any())
        {
            context.Set<AuditLog>().AddRange(auditEntries);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private AuditLog CreateAuditEntry(EntityEntry entry)
    {
        var httpContext = _accessor.HttpContext;
        var userId = GetUserId(httpContext);
        var entityName = entry.Entity.GetType().Name;
        var action = entry.State switch
        {
            EntityState.Added => "Creation",
            EntityState.Modified => "Modification",
            EntityState.Deleted => "Suppression",
            _ => "Inconnu"
        };

        return new AuditLog
        {
            DateEvenement = DateTime.UtcNow,
            UtilisateurId = userId,
            Action = $"{action}_{entityName}",
            TableConcernee = entityName,
            EnregistrementId = GetEntityId(entry),
            ValeurAvant = entry.State == EntityState.Modified ? GetOldValues(entry) : null,
            ValeurApres = entry.State != EntityState.Deleted ? GetNewValues(entry) : null,
            AdresseIP = httpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = httpContext?.Request.Headers["User-Agent"].ToString(),
            Details = $"Entité {entityName} — {action}"
        };
    }

    private int? GetUserId(HttpContext? httpContext)
    {
        if (httpContext?.User?.Identity?.IsAuthenticated != true) return null;

        var idClaim = httpContext.User.FindFirst(
            System.Security.Claims.ClaimTypes.NameIdentifier)
            ?? httpContext.User.FindFirst("sub");

        return int.TryParse(idClaim?.Value, out var id) ? id : null;
    }

    private int? GetEntityId(EntityEntry entry)
    {
        var idProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "Id");
        return idProp?.CurrentValue as int?;
    }

    private static string GetOldValues(EntityEntry entry)
    {
        var dict = entry.Properties
            .Where(p => p.IsModified)
            .ToDictionary(p => p.Metadata.Name, p => p.OriginalValue);

        return JsonSerializer.Serialize(dict);
    }

    private static string GetNewValues(EntityEntry entry)
    {
        var dict = entry.Properties
            .Where(p => p.IsModified || entry.State == EntityState.Added)
            .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue);

        return JsonSerializer.Serialize(dict);
    }
}

