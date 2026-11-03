using AkarangaBankMvc.Web.Data;
using AkarangaBankMvc.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace AkarangaBankMvc.Web.Services;

public static class AdminInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var context =
            services.GetRequiredService<AkarangaBankContext>();

        var userManager =
            services.GetRequiredService<UserManager<ApplicationUser>>();

        var roleManager =
            services.GetRequiredService<RoleManager<ApplicationRole>>();

        var configuration =
            services.GetRequiredService<IConfiguration>();

        var environment =
            services.GetRequiredService<IHostEnvironment>();

        var adminEmail = configuration["BootstrapAdmin:Email"]?.Trim();

        if (string.IsNullOrWhiteSpace(adminEmail) ||
            adminEmail == "mettez_votre_email_ici")
        {
            throw new InvalidOperationException(
                "Configure BootstrapAdmin:Email avec une adresse valide.");
        }

        var admin = await userManager.FindByEmailAsync(adminEmail);

        var adminPassword = configuration["BootstrapAdmin:Password"];

        if (admin is null && string.IsNullOrWhiteSpace(adminPassword))
        {
            throw new InvalidOperationException(
                "Configure BootstrapAdmin:Password pour créer le premier administrateur.");
        }

        string[] roles =
        {
            "admin",
            "gestionnaire",
            "auditeur",
            "guichetier",
            "client"
        };

        foreach (var roleName in roles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var result = await roleManager.CreateAsync(
                    new ApplicationRole { Name = roleName });

                EnsureSucceeded(result, $"Création du rôle '{roleName}'");
            }
        }

        if (admin is null)
        {
            var sieges = await context.Agences
                .Where(a => a.EstSiege)
                .OrderBy(a => a.Id)
                .Take(2)
                .ToListAsync();

            if (sieges.Count > 1)
            {
                throw new InvalidOperationException(
                    "Plusieurs agences sont marquées comme siège. Corrige les données.");
            }

            var siege = sieges.SingleOrDefault();

            if (siege is null)
            {
                if (!environment.IsDevelopment())
                {
                    throw new InvalidOperationException(
                        "Aucune agence siège : initialise les agences avant de créer l'administrateur.");
                }

                var agenceBjm = await context.Agences
                    .SingleOrDefaultAsync(a => a.Code == "AKR-BJM");

                if (agenceBjm is not null)
                {
                    throw new InvalidOperationException(
                        "L'agence AKR-BJM existe, mais EstSiege est false. Vérifie les données.");
                }

                siege = new Agence
                {
                    Code = "AKR-BJM",
                    Nom = "Akaranga Bank Bujumbura",
                    Ville = "Bujumbura",
                    EstSiege = true
                };

                context.Agences.Add(siege);
                await context.SaveChangesAsync();
            }

            admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                NomComplet = "Administrateur Système",
                Role = "admin",
                AgenceId = siege.Id,
                EstActif = true
            };

            var creationResult =
                await userManager.CreateAsync(admin, adminPassword!);

            EnsureSucceeded(
                creationResult,
                "Création du premier administrateur");
        }

        if (!await userManager.IsInRoleAsync(admin, "admin"))
        {
            var roleResult =
                await userManager.AddToRoleAsync(admin, "admin");

            EnsureSucceeded(
                roleResult,
                "Attribution du rôle admin");
        }

        if (admin.Role != "admin")
        {
            admin.Role = "admin";

            var updateResult = await userManager.UpdateAsync(admin);

            EnsureSucceeded(
                updateResult,
                "Mise à jour du champ Role");
        }
    }

    private static void EnsureSucceeded(
        IdentityResult result,
        string operation)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = string.Join(
            "; ",
            result.Errors.Select(e => $"{e.Code}: {e.Description}"));

        throw new InvalidOperationException(
            $"{operation} a échoué : {errors}");
    }
}