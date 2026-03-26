using System.CommandLine;
using Iaet.Catalog;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Cli.Commands;

internal static class StreamsCommand
{
    internal static Command Create(IServiceProvider services)
    {
        var streamsCmd = new Command("streams", "Browse captured data streams");

        var listCmd = new Command("list", "List streams for a session");
        var sessionIdOption = new Option<Guid>("--session-id") { Description = "Session ID", Required = true };
        listCmd.Add(sessionIdOption);
        listCmd.SetAction(async (parseResult) =>
        {
            var sessionId = parseResult.GetRequiredValue(sessionIdOption);

            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            await db.Database.MigrateAsync().ConfigureAwait(false);
            var catalog = scope.ServiceProvider.GetRequiredService<IStreamCatalog>();
            var streams = await catalog.GetStreamsBySessionAsync(sessionId).ConfigureAwait(false);

            if (streams.Count == 0)
            {
                Console.WriteLine("No streams found for session.");
                return;
            }

            Console.WriteLine($"{"Protocol",-20} {"URL",-50} {"Started",-22} {"Metadata"}");
            Console.WriteLine(new string('-', 120));
            foreach (var s in streams)
            {
                var metaSummary = string.Join(", ", s.Metadata.Properties.Select(kv => $"{kv.Key}={kv.Value}"));
                Console.WriteLine($"{s.Protocol,-20} {s.Url,-50} {s.StartedAt:g,-22} {metaSummary}");
            }
        });

        var showCmd = new Command("show", "Show full details for a stream");
        var streamIdOption = new Option<Guid>("--stream-id") { Description = "Stream ID", Required = true };
        showCmd.Add(streamIdOption);
        showCmd.SetAction(async (parseResult) =>
        {
            var streamId = parseResult.GetRequiredValue(streamIdOption);

            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            await db.Database.MigrateAsync().ConfigureAwait(false);
            var catalog = scope.ServiceProvider.GetRequiredService<IStreamCatalog>();
            var stream = await catalog.GetStreamByIdAsync(streamId).ConfigureAwait(false);

            if (stream is null)
            {
                Console.WriteLine($"Stream {streamId} not found.");
                return;
            }

            Console.WriteLine($"ID:         {stream.Id}");
            Console.WriteLine($"Session:    {stream.SessionId}");
            Console.WriteLine($"Protocol:   {stream.Protocol}");
            Console.WriteLine($"URL:        {stream.Url}");
            Console.WriteLine($"Started:    {stream.StartedAt:u}");
            if (stream.EndedAt.HasValue)
            {
                Console.WriteLine($"Ended:      {stream.EndedAt:u}");
            }
            Console.WriteLine($"Tag:        {stream.Tag ?? "(none)"}");
            Console.WriteLine("Metadata:");
            foreach (var (key, value) in stream.Metadata.Properties)
            {
                Console.WriteLine($"  {key}: {value}");
            }

            if (stream.Frames is not null)
            {
                Console.WriteLine($"Frames:     {stream.Frames.Count}");
            }
        });

        var framesCmd = new Command("frames", "Show frame history for a stream");
        var framesStreamIdOption = new Option<Guid>("--stream-id") { Description = "Stream ID", Required = true };
        framesCmd.Add(framesStreamIdOption);
        framesCmd.SetAction(async (parseResult) =>
        {
            var streamId = parseResult.GetRequiredValue(framesStreamIdOption);

            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            await db.Database.MigrateAsync().ConfigureAwait(false);
            var catalog = scope.ServiceProvider.GetRequiredService<IStreamCatalog>();
            var stream = await catalog.GetStreamByIdAsync(streamId).ConfigureAwait(false);

            if (stream is null)
            {
                Console.WriteLine($"Stream {streamId} not found.");
                return;
            }

            if (stream.Frames is null || stream.Frames.Count == 0)
            {
                Console.WriteLine("No frame data captured for this stream.");
                return;
            }

            Console.WriteLine($"Stream {stream.Id} ({stream.Protocol}) — {stream.Frames.Count} frame(s)");
            Console.WriteLine($"{"#",-5} {"Direction",-12} {"Size",-10} {"Timestamp",-22} {"Preview"}");
            Console.WriteLine(new string('-', 90));
            var index = 0;
            foreach (var frame in stream.Frames)
            {
                var preview = frame.TextPayload is not null
                    ? frame.TextPayload.Length > 40
                        ? string.Concat(frame.TextPayload.AsSpan(0, 40), "...")
                        : frame.TextPayload
                    : frame.BinaryPayload is not null
                        ? $"[binary {frame.BinaryPayload.Length}b]"
                        : "(empty)";
                Console.WriteLine($"{index,-5} {frame.Direction,-12} {frame.SizeBytes,-10} {frame.Timestamp:g,-22} {preview}");
                index++;
            }
        });

        streamsCmd.Add(listCmd);
        streamsCmd.Add(showCmd);
        streamsCmd.Add(framesCmd);
        return streamsCmd;
    }
}
