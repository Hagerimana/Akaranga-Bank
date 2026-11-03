using AkarangaBankMvc.Web.Models;

namespace AkarangaBankMvc.Web.Services;

public interface IDetectionAvanceeService
{
    Task<ResultatSurveillance> DetecterStructuringAsync(
        int clientId,
        DateTime date);

    Task<ResultatSurveillance> DetecterSmurfingAsync(
        int clientId,
        DateTime date);

    Task<ResultatSurveillance> DetecterLayeringAsync(
        int clientId,
        DateTime date);

    Task<ResultatSurveillance> DetecterVelociteAnormaleAsync(
        int clientId,
        DateTime date);
}