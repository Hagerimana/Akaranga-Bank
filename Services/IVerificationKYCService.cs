// Chap 6
using AkarangaBankMvc.Web.Models;
using AkarangaBankMvc.Web.ViewModels;
namespace AkarangaBankMvc.Web.Services;
public interface IVerificationKYCService
{
 Task<VerificationKYC> CreateAsync(VerificationKYCCreateViewModel vm, int? verifieParId);
 Task<bool> ValiderAsync(int id, int verifieParId);
 Task<bool> RejeterAsync(int id, int verifieParId, string commentaire);
 Task<VerificationKYC?> GetByIdAsync(int id);
 Task<IReadOnlyList<VerificationKYC>> GetAllAsync(string? statut);
}
