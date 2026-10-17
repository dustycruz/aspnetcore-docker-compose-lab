namespace MyApi.Contracts;

public record ProductRequest(string? Name, string? Description, decimal Price);

public record ProductResponse(
    int Id,
    string Name,
    string? Description,
    decimal Price,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
