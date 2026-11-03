using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AkarangaBankMvc.Web.Models;
using AkarangaBankMvc.Web.ViewModels;

namespace AkarangaBankMvc.Web.Services;

public class UtilisateurService : IUtilisateurService
{
    private readonly UserManager<ApplicationUser> _userManager;
    public UtilisateurService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }
    public async Task<IEnumerable<ApplicationUser>> GetAllAsync()
    {
        // On utilise _userManager.Users (table Utilisateurs)
        return await _userManager.Users
        .Include(u => u.Agence)
        .OrderBy(u => u.NomComplet)
        .ToListAsync();
    }
    public async Task<ApplicationUser?> GetByIdAsync(int id)
    {
        return await _userManager.Users
        .Include(u => u.Agence)
        .FirstOrDefaultAsync(u => u.Id == id);
    }
    public async Task<(bool Success, string? Error)>
   CreateAsync(UtilisateurCreateViewModel vm)
    {
        var user = new ApplicationUser
        {
            UserName = vm.Email,
            Email = vm.Email,
            EmailConfirmed = true,
            NomComplet = vm.NomComplet,
            Role = vm.Role,
            AgenceId = vm.AgenceId,
            EstActif = true
        };
        var result = await _userManager.CreateAsync(user, vm.Password);
        if (!result.Succeeded)
            return (false, string.Join(", ", result.Errors.Select(e => e.Description)));

        var roleResult = await _userManager.AddToRoleAsync(user, vm.Role);

        if (!roleResult.Succeeded)
        {
            var errors = string.Join(", ",
                roleResult.Errors.Select(e => e.Description));

            return (false,
                $"L'utilisateur a été créé, mais l'attribution du rôle a échoué : {errors}");
        }

        return (true, null);
    }
    public async Task<(bool Success, string? Error)> ToggleActifAsync(int
   id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null) return (false, "Utilisateur introuvable.");
        user.EstActif = !user.EstActif;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return (false, string.Join(", ", result.Errors.Select(e => e.Description)));

        if (!user.EstActif)
            await _userManager.UpdateSecurityStampAsync(user);

        return (true, null);
    }
    public async Task<(bool Success, string? Error)> DeverrouillerAsync(int
   id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null) return (false, "Utilisateur introuvable.");

        if (await _userManager.IsLockedOutAsync(user))
        {
            await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.ResetAccessFailedCountAsync(user);
        }
        return (true, null);
    }
    public async Task<(bool Success, string? Error)> ResetPasswordAsync(int
   id, string newPassword)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null) return (false, "Utilisateur introuvable.");
        var token = await
       _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token,
       newPassword);
        return result.Succeeded
        ? (true, null)
        : (false, string.Join(", ", result.Errors.Select(e =>
       e.Description)));
    }
    public async Task<(bool Success, string? Error)> AssignRoleAsync(int id, string role)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null) return (false, "Utilisateur introuvable.");
        var currentRoles = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, currentRoles);
        await _userManager.AddToRoleAsync(user, role);
        user.Role = role;
        var result = await _userManager.UpdateAsync(user);

        return result.Succeeded
            ? (true, null)
            : (false, string.Join(", ",
                result.Errors.Select(e => e.Description)));
    }
}