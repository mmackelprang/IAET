using System.Globalization;
using Iaet.DukeEnergy.Abstractions;
using Iaet.DukeEnergy.Models;

namespace Iaet.DukeEnergy;

/// <summary>
/// Combines the account-scoped outage status with nearby and county-level outage data to answer
/// "is my power out, and is it just me?".
/// </summary>
/// <remarks>
/// Each source is optional and failures are isolated: a missing endpoint profile or an outage-map
/// error degrades one section of the answer into a note rather than failing the whole request.
/// </remarks>
public sealed class HomeOutageService : IHomeOutageService
{
    private readonly IOutageMapClient    _outageMap;
    private readonly IOutageReportClient _reportClient;
    private readonly DukeEnergyOptions   _options;
    private readonly TimeProvider        _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="HomeOutageService"/> class.</summary>
    /// <param name="outageMap">Public outage-map reader.</param>
    /// <param name="reportClient">Account-scoped outage-report client.</param>
    /// <param name="options">Client configuration.</param>
    /// <param name="timeProvider">Clock used for timestamps; defaults to the system clock.</param>
    public HomeOutageService(
        IOutageMapClient outageMap,
        IOutageReportClient reportClient,
        DukeEnergyOptions options,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(outageMap);
        ArgumentNullException.ThrowIfNull(reportClient);
        ArgumentNullException.ThrowIfNull(options);

        _outageMap    = outageMap;
        _reportClient = reportClient;
        _options      = options;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<HomeOutageStatus> GetHomeStatusAsync(CancellationToken cancellationToken = default)
    {
        var home  = _options.Home;
        var notes = new List<string>();

        var account       = await TryGetAccountStatusAsync(notes, cancellationToken).ConfigureAwait(false);
        var neighborhood  = await TryGetNeighborhoodAsync(notes, cancellationToken).ConfigureAwait(false);
        var county        = await TryGetCountyAsync(notes, cancellationToken).ConfigureAwait(false);

        return new HomeOutageStatus(
            home.Label,
            _timeProvider.GetUtcNow(),
            account,
            neighborhood,
            county,
            notes);
    }

    private async Task<AccountOutageStatus?> TryGetAccountStatusAsync(
        List<string> notes,
        CancellationToken cancellationToken)
    {
        if (!_reportClient.IsConfigured)
        {
            notes.Add(_reportClient.ConfigurationProblem
                ?? "Account-scoped outage status is unavailable: the outage-report flow is not configured.");
            return null;
        }

        var accountNumber = _options.Home.AccountNumber;

        if (string.IsNullOrWhiteSpace(accountNumber))
        {
            if (string.IsNullOrWhiteSpace(_options.Home.PhoneNumber))
            {
                notes.Add("Account-scoped outage status is unavailable: configure DukeEnergy:Home:AccountNumber "
                        + "or DukeEnergy:Home:PhoneNumber.");
                return null;
            }

            try
            {
                var lookup = await _reportClient
                    .LookupAccountByPhoneAsync(_options.Home.PhoneNumber, cancellationToken)
                    .ConfigureAwait(false);

                if (!lookup.Found || string.IsNullOrWhiteSpace(lookup.AccountNumber))
                {
                    notes.Add("No Duke Energy account matched the configured phone number.");
                    return null;
                }

                accountNumber = lookup.AccountNumber;
            }
            catch (HttpRequestException ex)
            {
                notes.Add($"Account lookup failed: {ex.Message}");
                return null;
            }
            catch (InvalidOperationException ex)
            {
                notes.Add($"Account lookup failed: {ex.Message}");
                return null;
            }
        }

        try
        {
            return await _reportClient
                .GetExistingOutageAsync(accountNumber, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            notes.Add($"Account outage status failed: {ex.Message}");
            return null;
        }
        catch (InvalidOperationException ex)
        {
            notes.Add($"Account outage status failed: {ex.Message}");
            return null;
        }
    }

    private async Task<NeighborhoodOutageReport?> TryGetNeighborhoodAsync(
        List<string> notes,
        CancellationToken cancellationToken)
    {
        var home = _options.Home;

        if (home.Latitude is null || home.Longitude is null)
        {
            notes.Add("Neighborhood outages are unavailable: configure DukeEnergy:Home:Latitude and "
                    + "DukeEnergy:Home:Longitude.");
            return null;
        }

        try
        {
            return await _outageMap
                .GetNeighborhoodAsync(
                    home.Latitude.Value,
                    home.Longitude.Value,
                    home.RadiusMiles,
                    home.Jurisdiction,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            notes.Add($"Neighborhood outage lookup failed: {ex.Message}");
            return null;
        }
    }

    private async Task<CountyOutageSummary?> TryGetCountyAsync(
        List<string> notes,
        CancellationToken cancellationToken)
    {
        var home = _options.Home;

        if (string.IsNullOrWhiteSpace(home.County))
        {
            return null;
        }

        try
        {
            var counties = await _outageMap
                .GetCountiesAsync(home.Jurisdiction, cancellationToken)
                .ConfigureAwait(false);

            var match = counties.FirstOrDefault(c =>
                string.Equals(c.CountyName, home.County, StringComparison.OrdinalIgnoreCase)
                && (string.IsNullOrWhiteSpace(home.State)
                    || string.Equals(c.State, home.State, StringComparison.OrdinalIgnoreCase)));

            if (match is null)
            {
                notes.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"County '{home.County}' was not present in the outage-map response."));
            }

            return match;
        }
        catch (HttpRequestException ex)
        {
            notes.Add($"County outage lookup failed: {ex.Message}");
            return null;
        }
    }
}
