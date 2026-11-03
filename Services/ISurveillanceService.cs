using AkarangaBankMvc.Web.Models;

namespace AkarangaBankMvc.Web.Services;

public interface ISurveillanceService
{
    Task<ResultatSurveillance> AnalyserTransactionAsync(
        int compteId,
        decimal montant,
        string typeOperation,
        DateTime date);

    Task<NiveauRisqueClient> EvaluerRisqueClientAsync(int clientId);
}