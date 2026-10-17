using MyApi.Contracts;

namespace MyApi.Services;

public static class ProductValidator
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 500;

    /// <summary>Returns validation errors keyed by field name; an empty dictionary means the request is valid.</summary>
    public static Dictionary<string, string[]> Validate(ProductRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors[nameof(ProductRequest.Name)] = ["Name is required."];
        }
        else if (request.Name.Trim().Length > NameMaxLength)
        {
            errors[nameof(ProductRequest.Name)] = [$"Name must be {NameMaxLength} characters or fewer."];
        }

        if (request.Description is { Length: > DescriptionMaxLength })
        {
            errors[nameof(ProductRequest.Description)] = [$"Description must be {DescriptionMaxLength} characters or fewer."];
        }

        if (request.Price < 0)
        {
            errors[nameof(ProductRequest.Price)] = ["Price cannot be negative."];
        }

        return errors;
    }
}
