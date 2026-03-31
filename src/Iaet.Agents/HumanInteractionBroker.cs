using Iaet.Core.Models;

namespace Iaet.Agents;

public sealed class HumanInteractionBroker(TextReader? input = null, TextWriter? output = null)
{
    private readonly TextReader _input = input ?? Console.In;
    private readonly TextWriter _output = output ?? Console.Out;

    public async Task RequestActionAsync(HumanActionRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await _output.WriteLineAsync().ConfigureAwait(false);
        await _output.WriteLineAsync($"[Action Required] {request.Action}").ConfigureAwait(false);
        await _output.WriteLineAsync($"  Reason: {request.Reason}").ConfigureAwait(false);
        if (request.Urgency != "normal")
            await _output.WriteLineAsync($"  Urgency: {request.Urgency}").ConfigureAwait(false);
        await _output.WriteAsync("  Press Enter when done: ").ConfigureAwait(false);
        await _input.ReadLineAsync(ct).ConfigureAwait(false);
    }

    public async Task<bool> RequestConfirmationAsync(string prompt, bool defaultYes = false, CancellationToken ct = default)
    {
        var hint = defaultYes ? "[Y/n]" : "[y/N]";
        await _output.WriteAsync($"{prompt} {hint} ").ConfigureAwait(false);
        var response = (await _input.ReadLineAsync(ct).ConfigureAwait(false))?.Trim().ToUpperInvariant() ?? string.Empty;
        if (string.IsNullOrEmpty(response))
            return defaultYes;
        return response == "Y" || response == "YES";
    }
}
