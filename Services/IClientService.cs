// Chap 5
using AkarangaBankMvc.Web.Models;
using AkarangaBankMvc.Web.ViewModels;
using AkarangaBank.Web.ViewModels;

namespace AkarangaBankMvc.Web.Services;

public interface IClientService
{
    Task<ClientListViewModel> GetPagedAsync(string? recherche, int? agenceId, string? statut, int page, int taille);
    Task<ClientDetailsViewModel?> GetDetailsAsync(int id);
    Task<Client?> GetByIdAsync(int id);
    Task<Client> CreateAsync(ClientCreateViewModel vm);
    Task<bool> UpdateAsync(ClientEditViewModel vm);
    Task<bool> DeleteAsync(int id);
    Task<bool> NumeroClientExisteAsync(string numeroClient, int? excludeId = null);
    Task<bool> NumeroCNIExisteAsync(string numeroCNI, int? excludeId = null);
}