using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Catalog;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIaetCatalog(this IServiceCollection services,
        string connectionString = "DataSource=catalog.db")
    {
        services.AddDbContext<CatalogDbContext>(options =>
            options.UseSqlite(connectionString));
        services.AddScoped<Iaet.Core.Abstractions.IEndpointCatalog, SqliteCatalog>();
        services.AddScoped<Iaet.Core.Abstractions.IStreamCatalog, SqliteStreamCatalog>();
        return services;
    }
}
