using System.Data;
using AkarangaBankMvc.Web.Data;
using AkarangaBankMvc.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AkarangaBankMvc.Web.Services;

public static class SeuilAMLInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var context =
            services.GetRequiredService<AkarangaBankContext>();

        await using var transaction =
            await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        var definitions = new[]
        {
            new
            {
                Code = "VIGILANCE",
                Libelle = "Vigilance renforcée — exercice",
                Montant = 5_000_000m,
                TypeOperation = "Toutes"
            },
            new
            {
                Code = "DECLARATION",
                Libelle = "Déclaration espèces dépôt — exercice",
                Montant = 10_000_000m,
                TypeOperation = "Depot"
            },
            new
            {
                Code = "DECLARATION",
                Libelle = "Déclaration espèces retrait — exercice",
                Montant = 10_000_000m,
                TypeOperation = "Retrait"
            },
            new
            {
                Code = "BLOCAGE",
                Libelle = "Blocage automatique — exercice",
                Montant = 50_000_000m,
                TypeOperation = "Toutes"
            }
        };

        foreach (var definition in definitions)
        {
            var existe = await context.SeuilsAML.AnyAsync(s =>
                s.Code == definition.Code &&
                s.TypeOperation == definition.TypeOperation &&
                s.Devise == "BIF");

            if (existe)
            {
                continue;
            }

            context.SeuilsAML.Add(new SeuilAML
            {
                Code = definition.Code,
                Libelle = definition.Libelle,
                Montant = definition.Montant,
                Devise = "BIF",
                TypeOperation = definition.TypeOperation,
                Actif = true,
                DateModification = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}