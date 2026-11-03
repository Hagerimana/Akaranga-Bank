namespace AkarangaBankMvc.Web.Services;

public interface IQrCodeService
{
    string GenerateQrCodeBase64(string text);
}