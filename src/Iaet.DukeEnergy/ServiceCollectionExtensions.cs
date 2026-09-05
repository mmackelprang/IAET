using Iaet.DukeEnergy.Abstractions;
using Iaet.DukeEnergy.Profiles;
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.DukeEnergy;

/// <summary>
/// Registers the Duke Energy outage client with a DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IOutageMapClient"/>, <see cref="IOutageReportClient"/>,
    /// <see cref="IAddressGeocoder"/>, <see cref="IAddressOutageService"/> and
    /// <see cref="IHomeOutageService"/>, each backed by a resilient <see cref="HttpClient"/>.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configure">Optional delegate to override <see cref="DukeEnergyOptions"/> defaults.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddDukeEnergy(
        this IServiceCollection services,
        Action<DukeEnergyOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new DukeEnergyOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);

        // Loaded once here rather than registered in the container: the profile is legitimately
        // absent until a capture has been merged in, and DI cannot register a null singleton.
        var profile = LoadProfile(options);

        services.AddHttpClient<IDukeEnergyCredentialProvider, ConfigJsonCredentialProvider>(
                    (http, sp) => new ConfigJsonCredentialProvider(
                        Configure(http, options),
                        options,
                        sp.GetService<TimeProvider>()))
                .AddStandardResilienceHandler();

        services.AddHttpClient<IOutageMapClient, OutageMapClient>(
                    (http, sp) => new OutageMapClient(
                        Configure(http, options),
                        options,
                        sp.GetRequiredService<IDukeEnergyCredentialProvider>(),
                        sp.GetService<TimeProvider>()))
                .AddStandardResilienceHandler();

        services.AddHttpClient<IOutageReportClient, TemplateOutageReportClient>(
                    (http, sp) => new TemplateOutageReportClient(
                        new TemplateRequestExecutor(Configure(http, options)),
                        options,
                        profile,
                        sp.GetService<TimeProvider>()))
                .AddStandardResilienceHandler();

        services.AddHttpClient<IAddressGeocoder, CensusAddressGeocoder>(
                    (http, sp) => new CensusAddressGeocoder(
                        Configure(http, options),
                        options,
                        sp.GetService<TimeProvider>()))
                .AddStandardResilienceHandler();

        services.AddSingleton<IAddressOutageService>(sp => new AddressOutageService(
            sp.GetRequiredService<IAddressGeocoder>(),
            sp.GetRequiredService<IOutageMapClient>(),
            options));

        services.AddSingleton<IHomeOutageService>(sp => new HomeOutageService(
            sp.GetRequiredService<IOutageMapClient>(),
            sp.GetRequiredService<IOutageReportClient>(),
            options,
            sp.GetService<TimeProvider>()));

        return services;
    }

    private static HttpClient Configure(HttpClient client, DukeEnergyOptions options)
    {
        client.Timeout = options.Timeout;
        return client;
    }

    /// <summary>
    /// Loads the endpoint profile if one is configured. A missing or malformed profile is not fatal:
    /// the report client reports it through <see cref="IOutageReportClient.ConfigurationProblem"/>
    /// so the outage-map half of the service still works.
    /// </summary>
    private static OutageReportProfile? LoadProfile(DukeEnergyOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Report.ProfilePath))
        {
            return null;
        }

        try
        {
            return OutageReportProfile.Load(options.Report.ProfilePath);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
