using MyApi.Contracts;
using MyApi.Services;

namespace MyApi.Tests;

public class ProductValidatorTests
{
    [Fact]
    public void Validate_ValidRequest_ReturnsNoErrors()
    {
        var errors = ProductValidator.Validate(new ProductRequest("Keyboard", "Mechanical", 79.99m));

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingName_ReturnsNameError(string? name)
    {
        var errors = ProductValidator.Validate(new ProductRequest(name, null, 1m));

        Assert.Contains(nameof(ProductRequest.Name), errors.Keys);
    }

    [Fact]
    public void Validate_NameTooLong_ReturnsNameError()
    {
        var name = new string('a', ProductValidator.NameMaxLength + 1);

        var errors = ProductValidator.Validate(new ProductRequest(name, null, 1m));

        Assert.Contains(nameof(ProductRequest.Name), errors.Keys);
    }

    [Fact]
    public void Validate_DescriptionTooLong_ReturnsDescriptionError()
    {
        var description = new string('d', ProductValidator.DescriptionMaxLength + 1);

        var errors = ProductValidator.Validate(new ProductRequest("Mouse", description, 1m));

        Assert.Contains(nameof(ProductRequest.Description), errors.Keys);
    }

    [Fact]
    public void Validate_NegativePrice_ReturnsPriceError()
    {
        var errors = ProductValidator.Validate(new ProductRequest("Mouse", null, -0.01m));

        Assert.Contains(nameof(ProductRequest.Price), errors.Keys);
    }
}
