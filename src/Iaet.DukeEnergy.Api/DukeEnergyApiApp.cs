using System.Globalization;
using Iaet.DukeEnergy.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.DukeEnergy.Api;

/// <summary>
/// Builds the Duke Energy outage REST service.
/// </summary>
/// <remarks>
/// <para>
/// The service has two halves. Everything under <c>/api/v1/outages</c> reads Duke Energy's public
/// outage map and works with no credentials. The account-scoped endpoints
/// (<c>/api/v1/accounts/...</c>, <c>/api/v1/outages/report</c>) need a captured endpoint profile
/// and return <c>503</c> with an explanation until one is supplied.
/// </para>
/// <para>
/// Configuration is read from the <c>DukeEnergy</c> section, which binds onto
/// <see cref="DukeEnergyOptions"/>.
/// </para>
/// </remarks>
public static class DukeEnergyApiApp
{
    /// <summary>Builds a <see cref="WebApplication"/> hosting the REST service.</summary>
    /// <param name="options">Host settings such as port and settings-file path.</param>
    /// <returns>A configured application, ready to run.</returns>
    public static WebApplication Build(DukeEnergyApiOptions? options = null)
    {
        var hostOptions = options ?? new DukeEnergyApiOptions();

        var builder = WebApplication.CreateBuilder();

        if (!string.IsNullOrWhiteSpace(hostOptions.SettingsPath))
        {
            builder.Configuration.AddJsonFile(hostOptions.SettingsPath, optional: false, reloadOnChange: false);
        }

        // Environment variables let the account number, phone number and any bearer token stay out
        // of settings files: DukeEnergy__Home__AccountNumber, and so on.
        builder.Configuration.AddEnvironmentVariables();

        var host = hostOptions.ListenOnAllInterfaces ? "*" : "localhost";
        builder.WebHost.UseUrls(string.Create(CultureInfo.InvariantCulture, $"http://{host}:{hostOptions.Port}"));

        var section = builder.Configuration.GetSection("DukeEnergy");
        builder.Services.AddDukeEnergy(section.Bind);

        builder.Services.AddProblemDetails();

        var app = builder.Build();

        app.UseExceptionHandler();
        app.UseStatusCodePages();

        app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
           .WithName("Health");

        OutagesApi.Map(app);
        HomeApi.Map(app);
        AccountsApi.Map(app);

        return app;
    }
}
