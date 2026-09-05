using System.Globalization;
using System.Threading.RateLimiting;
using Iaet.DukeEnergy.Abstractions;
using Iaet.DukeEnergy.Models;
using Iaet.DukeEnergy.Profiles;

namespace Iaet.DukeEnergy;

/// <summary>
/// Drives the account-scoped outage-report flow from a captured endpoint profile.
/// </summary>
/// <remarks>
/// <para>
/// Reporting an outage is a write against a real utility's operational systems, so this client is
/// deliberately hard to fire by accident: the flow is disabled unless
/// <see cref="OutageReportOptions.Enabled"/> is set, submission needs the separate
/// <see cref="OutageReportOptions.AllowSubmit"/> gate, submissions are capped per day, and a
/// submission is rejected outright unless it targets the account named in configuration.
/// </para>
/// </remarks>
public sealed class TemplateOutageReportClient : IOutageReportClient, IDisposable
{
    private readonly TemplateRequestExecutor _executor;
    private readonly DukeEnergyOptions       _options;
    private readonly OutageReportProfile?    _profile;
    private readonly TimeProvider            _timeProvider;
    private readonly FixedWindowRateLimiter  _submitLimiter;
    private readonly string?                 _configurationProblem;

    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="TemplateOutageReportClient"/> class.</summary>
    /// <param name="executor">Renders and sends profile templates.</param>
    /// <param name="options">Client configuration.</param>
    /// <param name="profile">The loaded endpoint profile, or <see langword="null"/> when none is configured.</param>
    /// <param name="timeProvider">Clock used for receipts; defaults to the system clock.</param>
    public TemplateOutageReportClient(
        TemplateRequestExecutor executor,
        DukeEnergyOptions options,
        OutageReportProfile? profile,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(options);

        _executor     = executor;
        _options      = options;
        _profile      = profile;
        _timeProvider = timeProvider ?? TimeProvider.System;

        _configurationProblem = DescribeProblem(options, profile);

        _submitLimiter = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit          = Math.Max(1, options.Report.MaxSubmissionsPerDay),
            Window               = TimeSpan.FromDays(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit           = 0,
        });
    }

    /// <inheritdoc />
    public bool IsConfigured => _configurationProblem is null;

    /// <inheritdoc />
    public string? ConfigurationProblem => _configurationProblem;

    private static string? DescribeProblem(DukeEnergyOptions options, OutageReportProfile? profile)
    {
        if (!options.Report.Enabled)
        {
            return "The outage-report flow is disabled. Set DukeEnergy:Report:Enabled to true to enable it.";
        }

        if (profile is null)
        {
            return "No outage-report endpoint profile is loaded. Set DukeEnergy:Report:ProfilePath to a profile "
                 + "captured with IAET — see docs/duke-energy-rest-interface.md.";
        }

        if (profile.IsPlaceholder)
        {
            return "The outage-report endpoint profile still contains REPLACE_ME placeholders. Fill it in from an "
                 + "IAET capture of the outage-report app — see docs/duke-energy-rest-interface.md.";
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<AccountLookupResult> LookupAccountByPhoneAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber);

        var (profile, template) = RequireTemplate(p => p.LookupAccount, "lookupAccount");

        var response = await _executor.ExecuteAsync(
            template,
            profile,
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["phoneNumber"] = NormalizePhone(phoneNumber),
                ["phoneRaw"]    = phoneNumber,
            },
            cancellationToken).ConfigureAwait(false);

        var accountNumber = response.Field("accountNumber");
        var found         = response.Flag("found") ?? (response.IsSuccess && !string.IsNullOrWhiteSpace(accountNumber));

        return new AccountLookupResult(
            found,
            accountNumber,
            response.Field("serviceAddress"),
            response.Fields);
    }

    /// <inheritdoc />
    public async Task<AccountOutageStatus> GetExistingOutageAsync(
        string accountNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountNumber);

        var (profile, template) = RequireTemplate(p => p.ExistingOutage, "existingOutage");

        var response = await _executor.ExecuteAsync(
            template,
            profile,
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["accountNumber"] = accountNumber,
            },
            cancellationToken).ConfigureAwait(false);

        var outageId = response.Field("outageId");
        var active   = response.Flag("hasActiveOutage") ?? !string.IsNullOrWhiteSpace(outageId);

        return new AccountOutageStatus(
            accountNumber,
            active,
            outageId,
            response.Field("status"),
            response.Field("cause"),
            response.Timestamp("reportedAt"),
            response.Timestamp("estimatedRestorationAt"),
            response.Fields);
    }

    /// <inheritdoc />
    public async Task<OutageReportReceipt> SubmitReportAsync(
        OutageReportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.AccountNumber);

        if (!_options.Report.AllowSubmit)
        {
            throw new InvalidOperationException(
                "Submitting outage reports is disabled. Set DukeEnergy:Report:AllowSubmit to true to enable it.");
        }

        // Filing an outage report against someone else's account would be both wrong and useless,
        // so the client only ever reports on the account it is configured for.
        var configured = _options.Home.AccountNumber;
        if (!string.IsNullOrWhiteSpace(configured)
            && !string.Equals(configured.Trim(), request.AccountNumber.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Refusing to report an outage for account '{request.AccountNumber}': this client is configured for a different account."));
        }

        var (profile, template) = RequireTemplate(p => p.SubmitReport, "submitReport");

        var submittedAt = _timeProvider.GetUtcNow();

        if (_options.Report.DryRun)
        {
            return new OutageReportReceipt(
                false,
                null,
                "Dry run: the report was rendered and validated but not sent.",
                submittedAt,
                true,
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase));
        }

        using var lease = _submitLimiter.AttemptAcquire(1);
        if (!lease.IsAcquired)
        {
            throw new InvalidOperationException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Outage-report submission limit reached ({_options.Report.MaxSubmissionsPerDay} per day)."));
        }

        var response = await _executor.ExecuteAsync(
            template,
            profile,
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["accountNumber"] = request.AccountNumber,
                ["phoneNumber"]   = NormalizePhone(request.PhoneNumber ?? _options.Home.PhoneNumber ?? string.Empty),
                ["comments"]      = request.Comments ?? string.Empty,
                ["email"]         = request.ContactEmail ?? string.Empty,
            },
            cancellationToken).ConfigureAwait(false);

        var confirmation = response.Field("confirmationNumber");

        return new OutageReportReceipt(
            response.Flag("accepted") ?? response.IsSuccess,
            confirmation,
            response.Field("message"),
            submittedAt,
            false,
            response.Fields);
    }

    private (OutageReportProfile Profile, RequestTemplate Template) RequireTemplate(
        Func<OutageReportProfile, RequestTemplate?> selector,
        string name)
    {
        if (_configurationProblem is not null || _profile is null)
        {
            throw new InvalidOperationException(_configurationProblem ?? "No outage-report endpoint profile is loaded.");
        }

        var template = selector(_profile)
            ?? throw new InvalidOperationException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The outage-report endpoint profile has no '{name}' template. Capture that request with IAET and add it to the profile."));

        return (_profile, template);
    }

    /// <summary>Reduces a phone number to its digits, which is what the flow submits.</summary>
    private static string NormalizePhone(string phoneNumber)
        => string.Concat(phoneNumber.Where(char.IsAsciiDigit));

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _submitLimiter.Dispose();
    }
}
