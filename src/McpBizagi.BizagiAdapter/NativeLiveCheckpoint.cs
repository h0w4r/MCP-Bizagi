using System.Security.Cryptography;
using System.Windows.Forms;
using McpBizagi.Contracts;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    public sealed partial class LiveSession
    {
        private LiveCheckpoint Checkpoint(LiveSessionRequest request, Action markDispatched)
        {
            object model = Get(form, "DiagramModel");
            string path = NativeLivePaths.RequireWorkingCopy(engine.workRoot, Text(model, "Path"));
            object info = Get(model, "ModelInfo");
            if ((bool)Get(info, "IsInCollaboration"))
                throw new NotSupportedException("Managed checkpoints are local-file operations; cloud collaboration is not supported.");
            // Keep the complete pre-save container, including content absent from
            // our typed snapshots. Never restore over a newer working copy blindly.
            byte[] previous = ReadStable(path);
            string previousRevision = Hash(previous);
            if (previousRevision != request.ExpectedDiskRevision)
                throw new IOException("Working-copy disk revision conflict before checkpoint.");
            string prefix = Path.Combine(engine.workRoot, request.OperationId.ToLowerInvariant());
            string backup = prefix + ".before.bpm", artifact = prefix + ".checkpoint.bpm";
            WriteNewArtifact(backup, previous);
            if (File.Exists(artifact)) throw new IOException("Checkpoint artifact identity already exists; inspect the original operation instead of replaying it.");
            // Stop the companion's own autosave timer during the explicit save.
            // It remains off; the operator can re-enable it in this dedicated editor.
            Call(form, "DisableAutoSave");
            if (Hash(ReadStable(path)) != previousRevision)
                throw new IOException("Working copy changed while preparing its checkpoint.");
            markDispatched();
            // The lower native Save retains vendor validation and authentication.
            // Avoid the outer toolbar Save, which calls Presenter.Focus().
            object result = Call(form, "Save", model, Get(manager, "PersistenceManager"), true, false, false, false)!;
            if (!Equals(result, DialogResult.OK))
                throw new InvalidOperationException("Native checkpoint Save did not complete: " + result);
            if ((bool)Get(manager, "HaveChanged"))
                throw new InvalidOperationException("Native Save returned without a clean working copy.");
            byte[] saved = ReadStable(path);
            WriteNewArtifact(artifact, saved);
            return new LiveCheckpoint { ArtifactPath = artifact, Revision = Hash(saved), PreviousArtifactPath = backup,
                PreviousRevision = previousRevision, DestinationPublished = false };
        }

        private static byte[] ReadStable(string path)
        {
            // Deny concurrent writes/replacements while taking each durable image.
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (input.Length <= 0 || input.Length > 256L * 1024 * 1024) throw new IOException("Invalid native working-copy size.");
            using var memory = new MemoryStream(); input.CopyTo(memory); return memory.ToArray();
        }

        private static string Hash(byte[] bytes)
        { using var hash = SHA256.Create(); return Hex(hash.ComputeHash(bytes)); }

        private static void WriteNewArtifact(string path, byte[] bytes)
        {
            string stage = path + ".tmp";
            using (var output = new FileStream(stage, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { output.Write(bytes, 0, bytes.Length); output.Flush(true); }
            File.Move(stage, path);
        }
    }
}
