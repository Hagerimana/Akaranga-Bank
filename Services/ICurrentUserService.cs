namespace AkarangaBankMvc.Web.Services;

public interface ICurrentUserService
{
    Task<int> GetUserIdAsync();

    Task<int> GetAgenceIdAsync();
}