using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using McpBizagi.Contracts;
using Newtonsoft.Json;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    /// <summary>Serial typed access to the actual open document. Transport disposal does not own its lifetime.</summary>
    public sealed partial class LiveSession : IDisposable
    {
        private readonly NativeEngine engine;
        private readonly Form form;
        private readonly object manager;
        private readonly string sessionId;
        private readonly SemaphoreSlim serial = new(1, 1);
        private readonly EventInfo commandEvent;
        private readonly Delegate commandHandler;
        private readonly Dictionary<string, string> receipts = new(StringComparer.OrdinalIgnoreCase);
        private readonly object pendingSync = new();
        private readonly HashSet<Action<Exception>> pending = new();
        private object? observedDocument;
        private long epoch;
        private bool uncertain;
        private volatile bool disposed;

        internal LiveSession(NativeEngine engine, Form form, string sessionId)
        {
            if (!Guid.TryParseExact(sessionId, "D", out var id) || id == Guid.Empty) throw new ArgumentException("Invalid session identity.");
            this.engine = engine; this.form = form; this.sessionId = sessionId;
            manager = form.GetType().GetField("_elementManager", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
            commandEvent = manager.GetType().GetEvent("CommandExecuted")!;
            commandHandler = Delegate.CreateDelegate(commandEvent.EventHandlerType!, this,
                GetType().GetMethod(nameof(OnNativeCommand), BindingFlags.Instance | BindingFlags.NonPublic)!);
            commandEvent.AddEventHandler(manager, commandHandler);
        }

        // Epoch prevents ABA: changing A->B->A still invalidates an old revision.
        private void OnNativeCommand(object sender, object args) => Interlocked.Increment(ref epoch);

        public async Task<LiveSessionReply> ExecuteAsync(LiveSessionRequest request, CancellationToken cancellation)
        {
            // Freeze caller-owned mutable DTOs before admission or fingerprinting.
            string payload = JsonConvert.SerializeObject(request);
            string fingerprint;
            using (var hash = SHA256.Create()) fingerprint = Hex(hash.ComputeHash(Encoding.UTF8.GetBytes(payload)));
            request = JsonConvert.DeserializeObject<LiveSessionRequest>(payload)!;
            LiveSessionProtocol.Validate(request);
            if (!request.SessionId.Equals(sessionId, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("The requested native session is not this process.");
            await serial.WaitAsync(cancellation).ConfigureAwait(false);
            bool dispatched = false;
            try
            {
                if (disposed) throw new ObjectDisposedException(nameof(LiveSession));
                if (receipts.TryGetValue(request.OperationId, out var known))
                {
                    if (known != fingerprint) return new LiveSessionReply { OperationId = request.OperationId, State = "rejected",
                        Code = "operation_identity_conflict", Message = "Operation identity was already used for another request; the original receipt is retained." };
                    try
                    {
                        var saved = JsonConvert.DeserializeObject<LiveSessionReply>(File.ReadAllText(ReceiptPath(request.OperationId)));
                        if (saved == null || !saved.OperationId.Equals(request.OperationId, StringComparison.OrdinalIgnoreCase)) throw new JsonException("Receipt identity mismatch.");
                        return saved;
                    }
                    catch (Exception error) when (error is IOException || error is JsonException || error is UnauthorizedAccessException)
                    { return new LiveSessionReply { OperationId = request.OperationId, State = "uncertain", Code = "receipt_unavailable", Message = "The recorded receipt cannot be read; the operation will not be replayed." }; }
                }
                if (receipts.Count >= 10000) return new LiveSessionReply { OperationId = request.OperationId, State = "rejected",
                    Code = "session_receipt_capacity", Message = "Session receipt capacity reached; preserve work and start a new session." };
                var before = await SynchronizeAsync(request.OperationId, "before", cancellation).ConfigureAwait(false);
                if (request.Action != "read" && (uncertain || before.Revision != request.ExpectedRevision))
                    throw new InvalidOperationException(uncertain ? "A previous native operation has an uncertain result; editing is locked for investigation." : "Live revision conflict.");
                if (request.Action == "read") return Retain(request, fingerprint, before, "completed", "live_read_completed");
                cancellation.ThrowIfCancellationRequested();
                // Record intent before dispatch. A disconnected client can query the same operation ID.
                File.WriteAllText(Path.Combine(engine.workRoot, request.OperationId + ".intent.json"), payload);
                object? undoBefore = null;
                LiveCheckpoint? checkpoint = null;
                await OnUi(() =>
                {
                    // Recheck after admission: native UI callbacks may have run since the first snapshot.
                    if (Snapshot().Revision != before.Revision) throw new InvalidOperationException("Live revision conflict before native dispatch.");
                    if (request.Action == "update")
                    {
                        object command = PrepareUpdate(request, before);
                        dispatched = true;
                        Call(form, "OnExecutingCommand", command);
                    }
                    else if (request.Action == "checkpoint")
                    {
                        // A checkpoint saves only the owner-staged working document.
                        // Publication to an original is a separate guarded transaction.
                        checkpoint = Checkpoint(request, () => dispatched = true);
                    }
                    else
                    {
                        object undo = Get(manager, "UndoList");
                        undoBefore = Optional(undo, request.Action == "undo" ? "UndoCommand" : "RedoCommand");
                        if (undoBefore == null) throw new InvalidOperationException("No native command is available for " + request.Action + ".");
                        dispatched = true;
                        Call(form, request.Action == "undo" ? "OnUndo" : "OnRedo");
                    }
                    return true;
                }, cancellation).ConfigureAwait(false);
                if (request.Action is "undo" or "redo")
                {
                    // Toolbar undo/redo returns before its Chromium callback. Keep the serial
                    // lease until the actual native history pointer changes. Never kill the editor.
                    int seconds = int.TryParse(Environment.GetEnvironmentVariable("MCP_BIZAGI_INACTIVITY_SECONDS"), out int n) && n >= 10 ? n : 120;
                    long lastEpoch = Interlocked.Read(ref epoch); var inactivity = Stopwatch.StartNew();
                    while (await OnUi(() => ReferenceEquals(Optional(Get(manager, "UndoList"), request.Action == "undo" ? "UndoCommand" : "RedoCommand"), undoBefore), CancellationToken.None).ConfigureAwait(false))
                    {
                        long current = Interlocked.Read(ref epoch);
                        if (current != lastEpoch) { lastEpoch = current; inactivity.Restart(); }
                        if (inactivity.Elapsed.TotalSeconds > seconds) throw new TimeoutException("Native history callback has made no observable progress.");
                        await Task.Delay(100).ConfigureAwait(false);
                    }
                    bool matches = await OnUi(() => ReferenceEquals(Optional(Get(manager, "UndoList"), request.Action == "undo" ? "RedoCommand" : "UndoCommand"), undoBefore), CancellationToken.None).ConfigureAwait(false);
                    if (!matches) throw new InvalidOperationException("Native history changed concurrently; the requested undo/redo result is uncertain.");
                }
                var after = await SynchronizeAsync(request.OperationId, "after", CancellationToken.None).ConfigureAwait(false);
                if (request.Action == "update")
                    foreach (var patch in request.Changes)
                    {
                        var value = after.Elements.Single(e => e.Id.Equals(patch.ElementId, StringComparison.OrdinalIgnoreCase));
                        if ((patch.Name != null && value.Name != patch.Name) || (patch.Documentation != null && value.Documentation != patch.Documentation))
                            throw new InvalidOperationException("Native batch readback differs from the requested properties.");
                    }
                if (checkpoint != null)
                {
                    if (after.Dirty || after.DiskRevision != checkpoint.Revision)
                        throw new InvalidOperationException("The native working copy changed during checkpoint verification.");
                    checkpoint.DocumentRevision = after.Revision;
                }
                return Retain(request, fingerprint, after, "completed", checkpoint == null ? "live_native_command_completed" : "live_working_copy_checkpointed", checkpoint);
            }
            catch (Exception error)
            {
                if (dispatched) uncertain = true;
                var reply = new LiveSessionReply { OperationId = request.OperationId, State = dispatched ? "uncertain" : "rejected",
                    Code = dispatched ? "native_result_requires_review" : error is LiveEditorNotReadyException ? "live_editor_not_ready" : "live_request_rejected", Message = error.Message };
                // Diagnostics cannot cause a second dispatch or clear native unsaved state.
                receipts[request.OperationId] = fingerprint;
                File.WriteAllText(Path.Combine(engine.workRoot, request.OperationId + ".error.txt"), error.ToString());
                WriteReceipt(request.OperationId, reply);
                return reply;
            }
            finally { serial.Release(); }
        }

        private LiveSessionReply Retain(LiveSessionRequest request, string payload, LiveSessionSnapshot snapshot, string state, string code, LiveCheckpoint? checkpoint = null)
        {
            var reply = new LiveSessionReply { OperationId = request.OperationId, State = state, Code = code, Snapshot = snapshot, Checkpoint = checkpoint,
                Warnings = checkpoint == null ? Array.Empty<string>() : new[] { "This saves the managed working copy and clears undo according to native Save behavior. The original destination has not been published or independently verified." } };
            // Persist a receipt before claiming completion; retain every mutating operation identity.
            WriteReceipt(request.OperationId, reply);
            // Keep only compact fingerprints in memory; full model snapshots remain on disk.
            receipts[request.OperationId] = payload;
            return reply;
        }

        private string ReceiptPath(string id) => Path.Combine(engine.workRoot, id.ToLowerInvariant() + ".receipt.json");
        public async Task<LiveSessionReply> ReceiptAsync(string operationId, CancellationToken cancellation)
        {
            if (!Guid.TryParseExact(operationId, "D", out var id) || id == Guid.Empty) throw new ArgumentException("Invalid live operation identity.");
            await serial.WaitAsync(cancellation).ConfigureAwait(false);
            try
            {
                if (disposed) throw new ObjectDisposedException(nameof(LiveSession));
                if (!receipts.ContainsKey(operationId)) return new LiveSessionReply { OperationId = operationId, State = "unknown",
                    Code = "operation_not_recorded", Message = "No retained receipt exists for this operation in this native session. No request was dispatched by this query." };
                var reply = JsonConvert.DeserializeObject<LiveSessionReply>(File.ReadAllText(ReceiptPath(operationId)));
                if (reply == null || !reply.OperationId.Equals(operationId, StringComparison.OrdinalIgnoreCase)) throw new JsonException("Native receipt identity mismatch.");
                return reply;
            }
            finally { serial.Release(); }
        }
        private void WriteReceipt(string id, LiveSessionReply reply)
        {
            string path = ReceiptPath(id), stage = path + ".tmp";
            byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(reply));
            using (var file = new FileStream(stage, FileMode.Create, FileAccess.Write, FileShare.None))
            { file.Write(bytes, 0, bytes.Length); file.Flush(flushToDisk: true); }
            if (File.Exists(path)) File.Replace(stage, path, null); else File.Move(stage, path);
        }

        private Task<T> OnUi<T>(Func<T> action, CancellationToken cancellation)
        {
            var result = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            Action<Exception> fail = error => result.TrySetException(error);
            lock (pendingSync)
            {
                if (disposed || form.IsDisposed) { fail(new ObjectDisposedException(nameof(LiveSession))); return result.Task; }
                pending.Add(fail);
            }
            try
            {
                form.BeginInvoke((Action)(() =>
                {
                    try
                    {
                        if (disposed) throw new ObjectDisposedException(nameof(LiveSession));
                        cancellation.ThrowIfCancellationRequested(); result.TrySetResult(action());
                    }
                    catch (Exception error) { fail(error); }
                    finally { lock (pendingSync) pending.Remove(fail); }
                }));
            }
            catch (Exception error) { lock (pendingSync) pending.Remove(fail); fail(error); }
            return result.Task;
        }

        private LiveSessionSnapshot Snapshot()
        {
            if (Application.OpenForms.Cast<Form>().Any(f => f != form && f.Visible))
                throw new InvalidOperationException("Native startup or modal interaction is pending.");
            object model = Optional(form, "DiagramModel") ?? throw new InvalidOperationException("Native document is not ready.");
            if (Optional(form, "ActiveDiagramEditor") == null) throw new InvalidOperationException("Native diagram editor is not ready.");
            if (!ReferenceEquals(model, observedDocument)) { observedDocument = model; Interlocked.Increment(ref epoch); }
            object undo = Get(manager, "UndoList");
            var snapshot = new LiveSessionSnapshot
            {
                SessionId = sessionId, DocumentId = Text(model, "Id"), EngineVersion = engine.Version,
                Path = Text(model, "Path"), ActiveDiagramId = Text(manager, "ActiveDiagramId"),
                Dirty = (bool)Get(manager, "HaveChanged"), CanUndo = Optional(undo, "UndoCommand") != null,
                CanRedo = Optional(undo, "RedoCommand") != null, Visible = form.Visible,
                Elements = Graph(model).Select(Describe).ToArray(), Metadata = engine.Metadata(model),
                Documentation = engine.Documentation(model), DiagramState = engine.DiagramState(model)
            };
            string content = JsonConvert.SerializeObject(snapshot);
            using var sha = SHA256.Create();
            snapshot.Revision = sessionId + ":" + Interlocked.Read(ref epoch) + ":" + Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(content)));
            if (File.Exists(snapshot.Path))
            {
                using var file = new FileStream(snapshot.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                snapshot.DiskRevision = Hex(sha.ComputeHash(file));
            }
            return snapshot;
        }

        private object PrepareUpdate(LiveSessionRequest request, LiveSessionSnapshot before)
        {
            object model = Get(form, "DiagramModel");
            var graph = Graph(model).ToDictionary(e => Text(e.Value, "Id"), StringComparer.OrdinalIgnoreCase);
            object batch = New(engine.Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.EventArgs.CollectionCommandEventArgs"), "CollectionCommand", Guid.Parse(before.ActiveDiagramId));
            Set(batch, "BackwardUndo", true);
            foreach (var patch in request.Changes)
            {
                if (!graph.TryGetValue(patch.ElementId, out var entry) || entry.DiagramId != before.ActiveDiagramId || Optional(entry.Value, "GraphicalProperties") == null)
                    throw new InvalidOperationException("Live property batches require graphical elements in the active diagram.");
                if (entry.Value.GetType().Name is "Collaboration" or "Participant")
                    throw new NotSupportedException("Diagram/pool properties use distinct native notification contracts.");
                object clone = Call(entry.Value, "Clone")!;
                if (patch.Name != null) Set(clone, "DisplayName", patch.Name);
                if (patch.Documentation != null) Set(clone, "Documentation", patch.Documentation);
                object args = New(engine.Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.EventArgs.GraphicalElementEventArgs"), "ChangeElementProperties", Guid.Parse(entry.DiagramId));
                Set(args, "GraphicalElement", clone);
                string parentId = entry.ParentId;
                while (graph.TryGetValue(parentId, out var parent))
                {
                    if (IsNativeSubProcess(parent.Value)) { Set(args, "SubProcessId", Guid.Parse(parentId)); break; }
                    parentId = parent.ParentId;
                }
                Call(Get(batch, "SubCommandEventArgs"), "Add", args);
            }
            return batch;
        }

        private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        public void Dispose()
        {
            // Only the native message-loop owner disposes this object, never an RPC connection.
            lock (pendingSync)
            {
                disposed = true;
                foreach (var fail in pending) fail(new ObjectDisposedException(nameof(LiveSession)));
                pending.Clear();
            }
            commandEvent.RemoveEventHandler(manager, commandHandler);
        }
    }
}
