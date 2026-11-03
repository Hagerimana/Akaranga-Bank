using AkarangaBankMvc.Web.Data;
using AkarangaBankMvc.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace AkarangaBankMvc.Web.Services;

/// <summary>
/// Initialise les produits de prêt utilisés par le chapitre 12.
/// L'opération est idempotente : les types déjà présents sont conservés.
/// </summary>
public static class TypePretInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var context =
            services.GetRequiredService<AkarangaBankContext>();

        var definitions = new[]
        {
            new TypePret
            {
                Code = "CONSO",
                Libelle = "Crédit à la consommation",
                MontantMin = 50_000m,
                MontantMax = 50_000_000m,
                DureeMinMois = 6,
                DureeMaxMois = 60,
                TauxMin = 10m,
                TauxMax = 18m
            },
            new TypePret
            {
                Code = "IMMO",
                Libelle = "Crédit immobilier",
                MontantMin = 5_000_000m,
                MontantMax = 500_000_000m,
                DureeMinMois = 60,
                DureeMaxMois = 240,
                TauxMin = 8m,
                TauxMax = 14m
            },
            new TypePret
            {
                Code = "AUTO",
                Libelle = "Crédit automobile",
                MontantMin = 1_000_000m,
                MontantMax = 80_000_000m,
                DureeMinMois = 12,
                DureeMaxMois = 84,
                TauxMin = 9m,
                TauxMax = 16m
            },
            new TypePret
            {
                Code = "ENTR",
                Libelle = "Crédit d'entreprise",
                MontantMin = 1_000_000m,
                MontantMax = 200_000_000m,
                DureeMinMois = 12,
                DureeMaxMois = 120,
                TauxMin = 10m,
                TauxMax = 20m
            },
            new TypePret
            {
                Code = "MICRO",
                Libelle = "Microcrédit",
                MontantMin = 10_000m,
                MontantMax = 5_000_000m,
                DureeMinMois = 1,
                DureeMaxMois = 24,
                TauxMin = 12m,
                TauxMax = 24m
            }
        };

        foreach (var definition in definitions)
        {
            var existe = await context.TypesPret.AnyAsync(t =>
                t.Code == definition.Code);

            if (existe)
            {
                continue;
            }

            definition.Actif = true;
            definition.DateCreation = DateTime.UtcNow;

            context.TypesPret.Add(definition);
        }

        await context.SaveChangesAsync();
    }
}
