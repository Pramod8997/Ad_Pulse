using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AdPulse.Api.Campaigns;

public static class CampaignEndpoints
{
    public static RouteGroupBuilder MapCampaignEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/app/campaigns").RequireAuthorization();
        group.MapGet("", ListAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapPost("", CreateAsync);
        group.MapPatch("/{id:guid}", UpdateAsync);
        return group;
    }

    private static async Task<IResult> ListAsync(
        [AsParameters] CampaignListQuery query,
        CampaignService service,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await service.ListAsync(query, cancellationToken));
        }
        catch (CampaignValidationException exception)
        {
            return Results.ValidationProblem(exception.Errors.ToDictionary());
        }
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        CampaignService service,
        CancellationToken cancellationToken)
    {
        var campaign = await service.GetAsync(id, cancellationToken);
        return campaign is null ? Results.NotFound() : Results.Ok(campaign);
    }

    private static async Task<IResult> CreateAsync(
        CampaignWriteRequest request,
        CampaignService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var campaign = await service.CreateAsync(request, cancellationToken);
            return Results.Created($"/app/campaigns/{campaign.Id:D}", campaign);
        }
        catch (CampaignValidationException exception)
        {
            return Results.ValidationProblem(exception.Errors.ToDictionary());
        }
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        CampaignWriteRequest request,
        CampaignService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var campaign = await service.UpdateAsync(id, request, cancellationToken);
            return campaign is null ? Results.NotFound() : Results.Ok(campaign);
        }
        catch (CampaignValidationException exception)
        {
            return Results.ValidationProblem(exception.Errors.ToDictionary());
        }
    }
}
