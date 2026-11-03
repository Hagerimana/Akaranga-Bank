using System;
using System.Threading.Tasks;
using AkarangaBankMvc.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AkarangaBankMvc.Web.Data;

public static class TypeCompteInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var context = services
            .GetRequiredService<AkarangaBankContext>();

        var types = new[]
        {
            new TypeCompte
            {
                Code = "COURANT",
                Libelle = "Compte courant",
                TauxInteret = 0m,
                SoldeMinimum = 0m,
                AutoriseDecouvert = false,
                DateCreation = DateTime.UtcNow
            },
            new TypeCompte
            {
                Code = "EPARGNE",
                Libelle = "Compte épargne",
                TauxInteret = 3.50m,
                SoldeMinimum = 10000m,
                AutoriseDecouvert = false,
                DateCreation = DateTime.UtcNow
            },
            new TypeCompte
            {
                Code = "CREDIT",
                Libelle = "Compte crédit",
                TauxInteret = 12m,
                SoldeMinimum = 0m,
                AutoriseDecouvert = true,
                DateCreation = DateTime.UtcNow
            }
        };

        foreach (var type in types)
        {
            var existe = await context.TypesCompte
                .AnyAsync(t => t.Code == type.Code);

            if (!existe)
            {
                context.TypesCompte.Add(type);
            }
        }

        await context.SaveChangesAsync();
    }
}