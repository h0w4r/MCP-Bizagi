using System.Diagnostics;
using System.Reflection;
using McpBizagi.Contracts;
using Newtonsoft.Json;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    public sealed partial class LiveSession
    {
        private sealed class BrowserSynchronization
        {
            public int Version { get; set; }
            public int Pending { get; set; }
            public long Started { get; set; }
            public long Completed { get; set; }
            public long Failed { get; set; }
            public long Epoch { get; set; }
            public bool Editing { get; set; }
            public PendingLabel[] Expected { get; set; } = Array.Empty<PendingLabel>();
        }
        private sealed class PendingLabel
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
        }

        private async Task<LiveSessionSnapshot> SynchronizeAsync(string operationId, string phase, CancellationToken cancellation)
        {
            string script;
            using (var stream = typeof(NativeEngine).Assembly.GetManifestResourceStream("McpBizagi.BizagiAdapter.NativeLiveSynchronization.js")
                ?? throw new InvalidOperationException("Native live synchronization resource is missing."))
            using (var reader = new StreamReader(stream)) script = reader.ReadToEnd();
            // Never synchronously block the STA while Chromium calls back into it.
            var observed = await EvaluateSynchronization(script, cancellation).ConfigureAwait(false);
            void Record(BrowserSynchronization state, bool complete) => File.AppendAllText(Path.Combine(engine.workRoot, operationId + ".synchronization.jsonl"),
                JsonConvert.SerializeObject(new { at = DateTime.UtcNow, operationId, phase, complete, evidence = state }) + Environment.NewLine);
            Record(observed, false);
            int seconds = int.TryParse(Environment.GetEnvironmentVariable("MCP_BIZAGI_INACTIVITY_SECONDS"), out int n) && n >= 10 ? n : 120;
            var inactivity = Stopwatch.StartNew();
            string activity = JsonConvert.SerializeObject(observed);
            while (true)
            {
                cancellation.ThrowIfCancellationRequested();
                if (observed.Version != 1 || observed.Failed != 0 || observed.Pending < 0)
                    throw new InvalidOperationException("The native browser callback barrier did not complete successfully.");
                var snapshot = await OnUi(Snapshot, cancellation).ConfigureAwait(false);
                bool labelsMatch = observed.Expected.All(label => snapshot.Elements.Any(e => e.Id.Equals(label.Id, StringComparison.OrdinalIgnoreCase) && e.Name == label.Name));
                // Native mutation callbacks use Invoke onto the editor STA. Awaiting
                // their promise plus this UI barrier observes the actual native change.
                if (observed.Pending == 0 && !observed.Editing && labelsMatch)
                {
                    var confirm = await EvaluateSynchronization("window.__mcpLiveSynchronizationV1.status()", cancellation).ConfigureAwait(false);
                    if (confirm.Pending == 0 && confirm.Failed == 0 && !confirm.Editing && confirm.Started == observed.Started && confirm.Epoch == observed.Epoch)
                    {
                        snapshot.EditorSynchronization = new LiveSynchronizationEvidence { BarrierVersion = confirm.Version,
                            EditorEpoch = confirm.Epoch, StartedCallbacks = confirm.Started, CompletedCallbacks = confirm.Completed,
                            PendingCallbacks = confirm.Pending, VerifiedPendingLabels = confirm.Expected.Length };
                        Record(confirm, true);
                        return snapshot;
                    }
                    observed = confirm;
                }
                string next = JsonConvert.SerializeObject(observed);
                if (next != activity) { activity = next; inactivity.Restart(); Record(observed, false); }
                if (inactivity.Elapsed.TotalSeconds > seconds)
                    throw new TimeoutException("No progress while synchronizing the native editor's pending callbacks.");
                await Task.Delay(100, cancellation).ConfigureAwait(false);
                observed = await EvaluateSynchronization("window.__mcpLiveSynchronizationV1.status()", cancellation).ConfigureAwait(false);
            }
        }

        private async Task<BrowserSynchronization> EvaluateSynchronization(string script, CancellationToken cancellation)
        {
            var evaluation = await OnUi(() =>
            {
                // These version-pinned fields identify the actual editor browser, not
                // a second renderer, another tab or an arbitrary client-selected object.
                object editor = Optional(form, "ActiveDiagramEditor") ?? throw new InvalidOperationException("Native editor is not ready.");
                object view = editor.GetType().GetField("wbDiagramEditor", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(editor)!;
                return (Task<string>)Call(view, "EvaluateScript", script)!;
            }, cancellation).ConfigureAwait(false);
            string result = await evaluation.ConfigureAwait(false);
            var state = JsonConvert.DeserializeObject<BrowserSynchronization>(result)
                ?? throw new InvalidOperationException("Native synchronization returned no structured evidence.");
            if (state.Version != 1 || state.Started < 0 || state.Completed < 0 || state.Failed < 0 || state.Pending < 0 || state.Epoch < 0 ||
                state.Completed > state.Started || state.Failed > state.Started - state.Completed ||
                state.Started - state.Completed - state.Failed != state.Pending || state.Expected == null ||
                state.Expected.Any(label => label == null || string.IsNullOrEmpty(label.Id) || label.Name == null))
                throw new InvalidOperationException("Native synchronization evidence is malformed or inconsistent.");
            return state;
        }
    }
}
