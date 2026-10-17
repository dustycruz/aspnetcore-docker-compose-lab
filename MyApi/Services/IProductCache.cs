using MyApi.Contracts;

namespace MyApi.Services;

public interface IProductCache
{
    Task<ProductResponse?> GetAsync(int id);

    Task SetAsync(ProductResponse product);

    Task RemoveAsync(int id);
}
