namespace AkarangaBankMvc.Web.Services.Pdf;

public interface IPdfService
{
    Task<byte[]> GenererReleveComptePdfAsync(int compteId, DateTime debut, DateTime fin);
    Task<byte[]> GenererAttestationSoldePdfAsync(int compteId);
    Task<byte[]> GenererContratPretPdfAsync(int pretId);
    Task<byte[]> GenererDeclarationCnrfPdfAsync(int declarationId);
    Task<byte[]> GenererRecuOperationPdfAsync(int operationId);
}

