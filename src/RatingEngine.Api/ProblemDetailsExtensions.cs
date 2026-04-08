using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace RatingEngine.Api;

internal static class ProblemDetailsExtensions
{
    public static IServiceCollection AddRatingProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails();
        return services;
    }

    public static IApplicationBuilder UseRatingProblemDetails(this IApplicationBuilder app)
    {
        app.UseExceptionHandler(exceptionApp =>
        {
            exceptionApp.Run(async context =>
            {
                var ex = context.Features.Get<IExceptionHandlerFeature>()?.Error;
                var (status, title, code) = MapException(ex);
                var problem = new ProblemDetails
                {
                    Status = status,
                    Title = title,
                    Detail = ex?.Message,
                    Instance = context.Request.Path
                };
                problem.Extensions["code"] = code;
                await Results.Problem(problem).ExecuteAsync(context);
            });
        });

        return app;
    }

    private static (int status, string title, string code) MapException(Exception? ex) => ex switch
    {
        OperationCanceledException => (499, "Request cancelled", "request_cancelled"),
        KeyNotFoundException => (404, "Resource not found", "resource_not_found"),
        InvalidOperationException => (422, "Invalid rating configuration", "rating_config_invalid"),
        ArgumentException => (400, "Invalid request", "request_invalid"),
        _ => (500, "Unexpected server error", "internal_error")
    };
}
