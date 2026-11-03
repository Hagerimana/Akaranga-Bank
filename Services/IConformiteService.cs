using AkarangaBankMvc.Web.Models;

namespace AkarangaBankMvc.Web.Services;

public interface IConformiteService
{
    Task<ResultatConformite> AnalyserTransactionAsync(
        int compteId,
        decimal montant,
        string typeOperation);

    Task<bool> DetecterFractionnementAsync(
        int compteId,
        decimal montant,
        DateTime date);

    Task<NiveauRisqueClient> EvaluerRisqueClientAsync(int clientId);

    Task<decimal> GetSeuilAMLAsync();
}