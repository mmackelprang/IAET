using FluentAssertions;
using Iaet.Cli.Commands;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;

namespace Iaet.Cli.Tests;

/// <summary>
/// Tests that <see cref="InvestigateCommand"/> registers correctly and exposes
/// the expected command metadata.
/// </summary>
public class InvestigateCommandTests
{
    // ── Command registration ───────────────────────────────────────────────────

    [Fact]
    public void Create_ReturnsCommandWithCorrectName()
    {
        var services = BuildMinimalServiceProvider();
        var cmd = InvestigateCommand.Create(services);

        cmd.Name.Should().Be("investigate");
    }

    [Fact]
    public void Create_ReturnsCommandWithNonEmptyDescription()
    {
        var services = BuildMinimalServiceProvider();
        var cmd = InvestigateCommand.Create(services);

        cmd.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Create_ReturnsCommandInstance()
    {
        var services = BuildMinimalServiceProvider();
        var cmd = InvestigateCommand.Create(services);

        cmd.Should().NotBeNull();
        cmd.Should().BeAssignableTo<Command>();
    }

    [Fact]
    public void Create_CanBeAddedToRootCommand()
    {
        var services = BuildMinimalServiceProvider();
        var cmd = InvestigateCommand.Create(services);
        var root = new RootCommand("Test root");

        var act = () => root.Add(cmd);

        act.Should().NotThrow();
        root.Subcommands.Should().Contain(c => c.Name == "investigate");
    }

    [Fact]
    public void Create_CommandHasNoSubcommands()
    {
        // The investigate command is a leaf command — it has no sub-commands,
        // it runs the entire wizard interactively from a single entry point.
        var services = BuildMinimalServiceProvider();
        var cmd = InvestigateCommand.Create(services);

        cmd.Subcommands.Should().BeEmpty();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a minimal service provider that satisfies the DI requirements of
    /// <see cref="InvestigateCommand.Create"/> at construction time (no services
    /// are actually resolved until the command action fires).
    /// </summary>
    private static ServiceProvider BuildMinimalServiceProvider()
    {
        // InvestigateCommand.Create only captures the IServiceProvider reference —
        // it does not resolve any services at construction time.
        // A plain ServiceCollection with no registrations is sufficient here.
        var services = new ServiceCollection();
        return services.BuildServiceProvider();
    }
}
