using FluentAssertions;
using Iaet.Agents;

namespace Iaet.Agents.Tests;

public sealed class InvestigationLogTests : IDisposable
{
    private readonly string _rootDir;
    private readonly InvestigationLog _log;

    public InvestigationLogTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "iaet-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(_rootDir, "proj"));
        _log = new InvestigationLog(_rootDir);
    }

    public void Dispose()
    {
        _log.Dispose();
        if (Directory.Exists(_rootDir))
            Directory.Delete(_rootDir, recursive: true);
    }

    [Fact]
    public async Task Append_creates_log_file()
    {
        await _log.AppendAsync("proj", "lead", "Started investigation");
        var logPath = Path.Combine(_rootDir, "proj", "investigation.log");
        File.Exists(logPath).Should().BeTrue();
    }

    [Fact]
    public async Task Append_writes_structured_entries()
    {
        await _log.AppendAsync("proj", "lead", "Round 1 started");
        await _log.AppendAsync("proj", "network-capture", "Found 12 endpoints");
        var lines = await File.ReadAllLinesAsync(Path.Combine(_rootDir, "proj", "investigation.log"));
        lines.Should().HaveCount(2);
        lines[0].Should().Contain("[lead]");
        lines[0].Should().Contain("Round 1 started");
        lines[1].Should().Contain("[network-capture]");
    }
}
