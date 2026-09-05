using FluentAssertions;
using Iaet.DukeEnergy.Models;
using Iaet.DukeEnergy.Profiles;

namespace Iaet.DukeEnergy.Tests;

public class TemplateOutageReportClientTests
{
    private static OutageReportProfile FilledProfile() => new(
        BaseUri: new Uri("https://example.test"),
        LookupAccount: new RequestTemplate(
            "POST",
            "/find",
            Body: """{"phone":"{{phoneNumber}}"}""",
            ResponseMap: new Dictionary<string, string> { ["accountNumber"] = "account.id" }),
        LookupAccountByNumber: new RequestTemplate(
            "GET",
            "/accounts/{{accountNumber}}",
            ResponseMap: new Dictionary<string, string> { ["serviceAddress"] = "account.address" }),
        ExistingOutage: new RequestTemplate(
            "GET",
            "/status?a={{accountNumber}}",
            ResponseMap: new Dictionary<string, string>
            {
                ["hasActiveOutage"] = "outage.active",
                ["outageId"]        = "outage.id",
                ["status"]          = "outage.status",
                ["serviceAddress"]  = "outage.address",
            }),
        SubmitReport: new RequestTemplate(
            "POST",
            "/report",
            Body: """{"a":"{{accountNumber}}","c":"{{comments}}"}""",
            ResponseMap: new Dictionary<string, string> { ["confirmationNumber"] = "confirmation" }));

    private static TemplateOutageReportClient Create(
        StubHttpMessageHandler handler,
        DukeEnergyOptions options,
        OutageReportProfile? profile)
        => new(new TemplateRequestExecutor(new HttpClient(handler)), options, profile);

    private static DukeEnergyOptions EnabledOptions(bool allowSubmit = false, string? accountNumber = null)
    {
        var options = new DukeEnergyOptions();
        options.Report.Enabled     = true;
        options.Report.AllowSubmit = allowSubmit;
        options.Home.AccountNumber = accountNumber;
        return options;
    }

    [Fact]
    public void IsConfigured_is_false_and_explained_when_the_flow_is_disabled()
    {
        using var client = Create(new StubHttpMessageHandler(), new DukeEnergyOptions(), FilledProfile());

        client.IsConfigured.Should().BeFalse();
        client.ConfigurationProblem.Should().Contain("Report:Enabled");
    }

    [Fact]
    public void IsConfigured_is_false_and_explained_when_no_profile_is_loaded()
    {
        using var client = Create(new StubHttpMessageHandler(), EnabledOptions(), null);

        client.IsConfigured.Should().BeFalse();
        client.ConfigurationProblem.Should().Contain("ProfilePath");
    }

    [Fact]
    public void IsConfigured_is_false_while_the_profile_still_has_placeholders()
    {
        var placeholder = new OutageReportProfile(
            BaseUri: new Uri("https://example.test"),
            LookupAccount: new RequestTemplate("POST", "/REPLACE_ME/find-account"));

        using var client = Create(new StubHttpMessageHandler(), EnabledOptions(), placeholder);

        client.IsConfigured.Should().BeFalse();
        client.ConfigurationProblem.Should().Contain("REPLACE_ME");
    }

    [Fact]
    public async Task LookupAccountByPhoneAsync_strips_formatting_from_the_phone_number()
    {
        var handler = new StubHttpMessageHandler().Respond("/find", """{"account":{"id":"ACC-1"}}""");
        using var client = Create(handler, EnabledOptions(), FilledProfile());

        var result = await client.LookupAccountByPhoneAsync("(919) 555-0100");

        result.Found.Should().BeTrue();
        result.AccountNumber.Should().Be("ACC-1");
        handler.RequestBodies[0].Should().Be("""{"phone":"9195550100"}""");
    }

    [Fact]
    public async Task LookupAccountByPhoneAsync_reports_not_found_when_no_account_comes_back()
    {
        var handler = new StubHttpMessageHandler().Respond("/find", """{"account":{}}""");
        using var client = Create(handler, EnabledOptions(), FilledProfile());

        var result = await client.LookupAccountByPhoneAsync("9195550100");

        result.Found.Should().BeFalse();
        result.AccountNumber.Should().BeNull();
    }

    [Fact]
    public async Task GetExistingOutageAsync_maps_the_outage_fields()
    {
        var handler = new StubHttpMessageHandler()
            .Respond("/status", """{"outage":{"active":true,"id":"OUT-7","status":"Crew en route"}}""");
        using var client = Create(handler, EnabledOptions(), FilledProfile());

        var status = await client.GetExistingOutageAsync("ACC-1");

        status.HasActiveOutage.Should().BeTrue();
        status.OutageId.Should().Be("OUT-7");
        status.Status.Should().Be("Crew en route");
        handler.Requests[0].RequestUri!.Query.Should().Be("?a=ACC-1");
    }

    [Fact]
    public async Task GetExistingOutageAsync_infers_no_outage_when_the_flag_and_id_are_absent()
    {
        var handler = new StubHttpMessageHandler().Respond("/status", """{"outage":{}}""");
        using var client = Create(handler, EnabledOptions(), FilledProfile());

        var status = await client.GetExistingOutageAsync("ACC-1");

        status.HasActiveOutage.Should().BeFalse();
    }

    [Fact]
    public async Task LookupAccountByNumberAsync_returns_the_authoritative_service_address()
    {
        var handler = new StubHttpMessageHandler()
            .Respond("/accounts/", """{"account":{"address":"123 Main St, Raleigh, NC 27601"}}""");
        using var client = Create(handler, EnabledOptions(), FilledProfile());

        var result = await client.LookupAccountByNumberAsync("ACC-1");

        result.ServiceAddress.Should().Be("123 Main St, Raleigh, NC 27601");
        handler.Requests[0].RequestUri!.AbsolutePath.Should().Be("/accounts/ACC-1");
    }

    [Fact]
    public async Task LookupAccountByNumberAsync_echoes_the_input_account_number_back()
    {
        var handler = new StubHttpMessageHandler().Respond("/accounts/", """{"account":{"address":"1 A St"}}""");
        using var client = Create(handler, EnabledOptions(), FilledProfile());

        var result = await client.LookupAccountByNumberAsync("ACC-1");

        result.AccountNumber.Should().Be("ACC-1", "the response does not repeat it, but the caller supplied it");
        result.Found.Should().BeTrue();
    }

    [Fact]
    public async Task LookupAccountByNumberAsync_explains_when_the_profile_lacks_that_template()
    {
        var profile = FilledProfile() with { LookupAccountByNumber = null };
        using var client = Create(new StubHttpMessageHandler(), EnabledOptions(), profile);

        var act = async () => await client.LookupAccountByNumberAsync("ACC-1");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*lookupAccountByNumber*");
    }

    [Fact]
    public async Task GetExistingOutageAsync_carries_the_service_address_when_the_profile_maps_it()
    {
        var handler = new StubHttpMessageHandler()
            .Respond("/status", """{"outage":{"active":true,"id":"OUT-7","address":"9 Elm St"}}""");
        using var client = Create(handler, EnabledOptions(), FilledProfile());

        var status = await client.GetExistingOutageAsync("ACC-1");

        status.ServiceAddress.Should().Be("9 Elm St");
    }

    [Fact]
    public async Task SubmitReportAsync_is_refused_unless_submission_is_explicitly_allowed()
    {
        using var client = Create(new StubHttpMessageHandler(), EnabledOptions(), FilledProfile());

        var act = async () => await client.SubmitReportAsync(new OutageReportRequest("ACC-1"));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*AllowSubmit*");
    }

    [Fact]
    public async Task SubmitReportAsync_refuses_an_account_other_than_the_configured_one()
    {
        var handler = new StubHttpMessageHandler().Respond("/report", """{"confirmation":"C1"}""");
        using var client = Create(handler, EnabledOptions(allowSubmit: true, accountNumber: "ACC-1"), FilledProfile());

        var act = async () => await client.SubmitReportAsync(new OutageReportRequest("ACC-OTHER"));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*different account*");
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task SubmitReportAsync_sends_the_report_and_returns_the_confirmation()
    {
        var handler = new StubHttpMessageHandler().Respond("/report", """{"confirmation":"C-42"}""");
        using var client = Create(handler, EnabledOptions(allowSubmit: true, accountNumber: "ACC-1"), FilledProfile());

        var receipt = await client.SubmitReportAsync(new OutageReportRequest("ACC-1", Comments: "transformer bang"));

        receipt.Accepted.Should().BeTrue();
        receipt.DryRun.Should().BeFalse();
        receipt.ConfirmationNumber.Should().Be("C-42");
        handler.RequestBodies[0].Should().Be("""{"a":"ACC-1","c":"transformer bang"}""");
    }

    [Fact]
    public async Task SubmitReportAsync_sends_nothing_in_dry_run_mode()
    {
        var options = EnabledOptions(allowSubmit: true, accountNumber: "ACC-1");
        options.Report.DryRun = true;

        var handler = new StubHttpMessageHandler().Respond("/report", """{"confirmation":"C-42"}""");
        using var client = Create(handler, options, FilledProfile());

        var receipt = await client.SubmitReportAsync(new OutageReportRequest("ACC-1"));

        receipt.DryRun.Should().BeTrue();
        receipt.Accepted.Should().BeFalse();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task SubmitReportAsync_enforces_the_daily_submission_cap()
    {
        var options = EnabledOptions(allowSubmit: true, accountNumber: "ACC-1");
        options.Report.MaxSubmissionsPerDay = 2;

        var handler = new StubHttpMessageHandler().Respond("/report", """{"confirmation":"C"}""");
        using var client = Create(handler, options, FilledProfile());

        await client.SubmitReportAsync(new OutageReportRequest("ACC-1"));
        await client.SubmitReportAsync(new OutageReportRequest("ACC-1"));

        var act = async () => await client.SubmitReportAsync(new OutageReportRequest("ACC-1"));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*limit reached*");
        handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task Operations_fail_with_the_configuration_problem_when_unconfigured()
    {
        using var client = Create(new StubHttpMessageHandler(), new DukeEnergyOptions(), null);

        var act = async () => await client.LookupAccountByPhoneAsync("9195550100");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Report:Enabled*");
    }
}
