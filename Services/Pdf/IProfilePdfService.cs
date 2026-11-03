namespace AkarangaBankMvc.Web.Services.Pdf;

public interface IProfilePdfService
{
    byte[] Generer(ProfilePdfModel model);
}
