using MyApi.Contracts;
using MyApi.Services;

namespace MyApi.Endpoints;

public static class ProductEndpoints
{
    public static RouteGroupBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/products").WithTags("Products");

        group.MapGet("/", async (ProductService service, CancellationToken ct) =>
            Results.Ok(await service.GetAllAsync(ct)));

        group.MapGet("/{id:int}", async (int id, ProductService service, HttpContext http, CancellationToken ct) =>
        {
            var (product, fromCache) = await service.GetByIdAsync(id, ct);
            if (product is null)
            {
                return Results.NotFound();
            }

            // Lets you observe the Redis cache-aside behaviour from curl or a browser.
            http.Response.Headers["X-Cache"] = fromCache ? "HIT" : "MISS";
            return Results.Ok(product);
        });

        group.MapPost("/", async (ProductRequest request, ProductService service, CancellationToken ct) =>
        {
            var errors = ProductValidator.Validate(request);
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            var created = await service.CreateAsync(request, ct);
            return Results.Created($"/api/products/{created.Id}", created);
        });

        group.MapPut("/{id:int}", async (int id, ProductRequest request, ProductService service, CancellationToken ct) =>
        {
            var errors = ProductValidator.Validate(request);
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            var updated = await service.UpdateAsync(id, request, ct);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        });

        group.MapDelete("/{id:int}", async (int id, ProductService service, CancellationToken ct) =>
            await service.DeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound());

        return group;
    }
}
