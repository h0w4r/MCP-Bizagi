using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Hosting;

namespace McpBizagi.Server;

public sealed record OperationView(string OperationId, string Kind, string State, string Phase,
    DateTimeOffset StartedAt, DateTimeOffset UpdatedAt, object? Result, string? Error);

/// <summary>Durable operation status; restarting the server never silently replays a write.</summary>
public sealed class Operations : IHostedService
{
    private sealed class Entry
    {
        public required OperationView View;
        public readonly CancellationTokenSource Cancellation = new();
        public readonly object Sync = new();
        public Task Completion = Task.CompletedTask;
    }
    private readonly ConcurrentDictionary<string, Entry> entries = new();
    private readonly string root;
    private readonly object lifecycle = new();
    private bool stopping;
    public Operations(ServerOptions options)
    {
        root = Path.Combine(Path.GetFullPath(options.State), "operations");
        Directory.CreateDirectory(root);
        foreach (var file in Directory.EnumerateFiles(root, "*.json"))
        {
            try
            {
                var view = JsonSerializer.Deserialize<OperationView>(File.ReadAllText(file));
                if (view == null) continue;
                if (view.State is "running" or "cancelling")
                    view = view with { State = "interrupted", Error = "Previous server stopped. Inspect artifacts before retrying.", UpdatedAt = DateTimeOffset.UtcNow };
                var entry = new Entry { View = view };
                entries[view.OperationId] = entry;
                Persist(entry);
            }
            catch (JsonException) { Console.Error.WriteLine("Ignoring unreadable operation journal: " + Path.GetFileName(file)); }
        }
    }
    private void Persist(Entry entry)
    {
        string file = Path.Combine(root, entry.View.OperationId + ".json");
        using (var stream = new FileStream(file + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, entry.View, new JsonSerializerOptions { WriteIndented = true });
            stream.Flush(flushToDisk: true);
        }
        File.Move(file + ".tmp", file, true);
    }
    public OperationView Start(string kind, Func<string, Action<string>, CancellationToken, Task<object>> action)
    {
        lock (lifecycle)
        {
            if (stopping) throw new InvalidOperationException("The MCP host is shutting down; new operations are not accepted.");
            string id = Guid.NewGuid().ToString("N");
            var now = DateTimeOffset.UtcNow;
            var entry = new Entry { View = new(id, kind, "running", "queued", now, now, null, null) };
            entries[id] = entry;
            Persist(entry);
            entry.Completion = Task.Run(async () =>
            {
                void Progress(string phase)
                {
                    lock (entry.Sync)
                    {
                        if (entry.View.State is not "running" and not "cancelling") return;
                        entry.View = entry.View with { Phase = phase, UpdatedAt = DateTimeOffset.UtcNow }; Persist(entry);
                    }
                }
                try
                {
                    object result = await action(id, Progress, entry.Cancellation.Token);
                    entry.Cancellation.Token.ThrowIfCancellationRequested();
                    lock (entry.Sync) entry.View = entry.View with { State = "completed", Phase = "finished", Result = result, UpdatedAt = DateTimeOffset.UtcNow };
                }
                catch (OperationCanceledException)
                { lock (entry.Sync) entry.View = entry.View with { State = "cancelled", Phase = "stopped", UpdatedAt = DateTimeOffset.UtcNow }; }
                catch (Exception error)
                { lock (entry.Sync) entry.View = entry.View with { State = "failed", Error = error.Message, UpdatedAt = DateTimeOffset.UtcNow }; }
                finally { lock (entry.Sync) Persist(entry); }
            });
            return entry.View;
        }
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        Task[] tasks;
        lock (lifecycle)
        {
            stopping = true;
            foreach (var entry in entries.Values) entry.Cancellation.Cancel();
            tasks = entries.Values.Select(e => e.Completion).ToArray();
        }
        // Owned-worker cleanup, not a total runtime deadline. No writes are replayed after shutdown.
        await Task.WhenAll(tasks);
    }
    public OperationView Get(string id)
    {
        if (!entries.TryGetValue(id, out var entry)) throw new KeyNotFoundException("Unknown operation ID.");
        lock (entry.Sync) return entry.View;
    }
    public OperationView Cancel(string id)
    {
        if (!entries.TryGetValue(id, out var entry)) throw new KeyNotFoundException("Unknown operation ID.");
        lock (entry.Sync)
        {
            if (entry.View.State is "running" or "cancelling")
            { entry.View = entry.View with { State = "cancelling", UpdatedAt = DateTimeOffset.UtcNow }; entry.Cancellation.Cancel(); Persist(entry); }
            return entry.View;
        }
    }
}
