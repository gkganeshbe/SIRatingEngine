using RatingEngine.Core;
using RatingEngine.Data.Admin;

namespace RatingEngine.Admin.Api.Endpoints;

public static class PipelineStepEndpoints
{
    public static IEndpointRouteBuilder MapPipelineStepEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/coverages/{coverageId:int}/pipeline").WithTags("Pipeline Steps");

        // GET /admin/coverages/{coverageId}/pipeline/steps
        group.MapGet("/steps", async (int coverageId, IPipelineStepAdminRepository repo, CancellationToken cancellationToken) =>
            Results.Ok(await repo.ListStepsAsync(coverageId, cancellationToken)));

        // POST /admin/coverages/{coverageId}/pipeline/steps
        // Body: AddPipelineStepRequest { Step: StepConfig, InsertAfterOrder?: int }
        group.MapPost("/steps", async (
            int coverageId,
            AddPipelineStepRequest req,
            IPipelineStepAdminRepository repo,
            CancellationToken cancellationToken) =>
        {
            var dbId = await repo.AddStepAsync(coverageId, req.Step, req.InsertAfterOrder, cancellationToken);
            return Results.Created($"/admin/coverages/{coverageId}/pipeline/steps/{req.Step.Id}", new { dbId, stepId = req.Step.Id });
        });

        // PUT /admin/coverages/{coverageId}/pipeline/steps/{stepId}
        group.MapPut("/steps/{stepId}", async (
            int coverageId,
            string stepId,
            StepConfig req,
            IPipelineStepAdminRepository repo,
            CancellationToken cancellationToken) =>
            await repo.UpdateStepAsync(coverageId, stepId, req, cancellationToken) ? Results.Ok() : Results.NotFound());

        // DELETE /admin/coverages/{coverageId}/pipeline/steps/{stepId}
        group.MapDelete("/steps/{stepId}", async (
            int coverageId,
            string stepId,
            IPipelineStepAdminRepository repo,
            CancellationToken cancellationToken) =>
            await repo.DeleteStepAsync(coverageId, stepId, cancellationToken) ? Results.NoContent() : Results.NotFound());

        // PUT /admin/coverages/{coverageId}/pipeline/reorder
        // Body: ReorderStepsRequest { OrderedStepIds: ["S1","S3","S2",...] }
        group.MapPut("/reorder", async (
            int coverageId,
            ReorderStepsRequest req,
            IPipelineStepAdminRepository repo,
            CancellationToken cancellationToken) =>
        {
            await repo.ReorderStepsAsync(coverageId, req.OrderedStepIds, cancellationToken);
            return Results.Ok();
        });

        return app;
    }
}
