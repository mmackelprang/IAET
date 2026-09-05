using FluentAssertions;
using Iaet.DukeEnergy.Profiles;

namespace Iaet.DukeEnergy.Tests;

public sealed class OutageReportProfileTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "iaet-duke-tests-" + Guid.NewGuid().ToString("N"));

    public OutageReportProfileTests() => Directory.CreateDirectory(_directory);

    private string WriteProfile(string json)
    {
        var path = Path.Combine(_directory, "profile.json");
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public void Load_reads_a_camel_case_profile_document()
    {
        var path = WriteProfile("""
        {
          "description": "captured 2026-09-05",
          "baseUri": "https://example.test",
          "defaultHeaders": { "Accept": "application/json" },
          "lookupAccount": {
            "method": "POST",
            "urlTemplate": "/find",
            "body": "{\"phone\":\"{{phoneNumber}}\"}",
            "responseMap": { "accountNumber": "account.id" }
          }
        }
        """);

        var profile = OutageReportProfile.Load(path);

        profile.Description.Should().Be("captured 2026-09-05");
        profile.BaseUri.Should().Be(new Uri("https://example.test"));
        profile.DefaultHeaders!["Accept"].Should().Be("application/json");
        profile.LookupAccount!.Method.Should().Be("POST");
        profile.LookupAccount.UrlTemplate.Should().Be("/find");
        profile.LookupAccount.ResponseMap!["accountNumber"].Should().Be("account.id");
        profile.IsPlaceholder.Should().BeFalse();
    }

    [Fact]
    public void Load_flags_the_shipped_template_as_a_placeholder()
    {
        var path = WriteProfile("""
        { "lookupAccount": { "method": "POST", "urlTemplate": "/REPLACE_ME/find-account" } }
        """);

        OutageReportProfile.Load(path).IsPlaceholder.Should().BeTrue();
    }

    [Fact]
    public void Load_treats_a_profile_with_no_lookup_template_as_a_placeholder()
    {
        var path = WriteProfile("""{ "baseUri": "https://example.test" }""");

        OutageReportProfile.Load(path).IsPlaceholder.Should().BeTrue();
    }

    [Fact]
    public void Load_throws_a_clear_error_for_a_missing_file()
    {
        var act = () => OutageReportProfile.Load(Path.Combine(_directory, "nope.json"));

        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void Load_throws_a_clear_error_for_malformed_json()
    {
        var path = WriteProfile("{ not json");

        var act = () => OutageReportProfile.Load(path);

        act.Should().Throw<InvalidOperationException>().WithMessage("*not valid JSON*");
    }

    [Fact]
    public void The_shipped_template_profile_parses_and_is_marked_as_a_placeholder()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "profiles", "duke-outage-report.template.json");

        File.Exists(path).Should().BeTrue("the sample profile ships alongside the library");

        var profile = OutageReportProfile.Load(path);

        profile.IsPlaceholder.Should().BeTrue();
        profile.SubmitReport.Should().NotBeNull();
        profile.ExistingOutage.Should().NotBeNull();
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }

        GC.SuppressFinalize(this);
    }
}
