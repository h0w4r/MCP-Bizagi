using System.Text.Json;
using McpBizagi.Contracts;
using McpBizagi.Core;

namespace McpBizagi.Server;

public sealed partial class NativeWorkflows
{
    public OperationView Align(string path, string expectedRevision, NativeAlignmentRequest alignment)
    {
        NativeAlignmentPolicy.Validate(alignment);
        var input = ReadNative(path);
        if (input.Revision != expectedRevision) throw new IOException("Native alignment source revision conflict.");
        var captured = JsonSerializer.Deserialize<NativeAlignmentRequest>(JsonSerializer.Serialize(alignment))!;
        return operations.Start("native_elements_align", async (id, progress, token) =>
        {
            string directory = CreateArtifactDirectory(id), source = Path.Combine(directory, "input.bpm"), output = Path.Combine(directory, "edited.bpm");
            await File.WriteAllBytesAsync(source, input.Bytes, token);
            File.WriteAllText(Path.Combine(directory, "alignment-request.json"), JsonSerializer.Serialize(captured));
            var before = await Execute(new EngineRequest { OperationId = id, Action = "exchange_read", InputPath = source }, RunDirectory(id, "source-reader"), progress, token);
            var expected = NativeAlignmentPolicy.Expected(before.Elements, captured);
            string editorDirectory = RunDirectory(id, "editor");
            var edited = await Execute(new EngineRequest { OperationId = id, Action = "align_save", InputPath = source, OutputPath = output, Alignment = captured, AlignmentExpected = expected },
                editorDirectory, progress, token);
            var reopened = await Execute(new EngineRequest { OperationId = id, Action = "exchange_read", InputPath = output }, RunDirectory(id, "reader"), progress, token);
            var graphDifferences = XpdlDocument.CompareGraph(edited.Elements, reopened.Elements);
            var metadataDifferences = XpdlDocument.CompareMetadata(edited, reopened);
            File.WriteAllText(Path.Combine(directory, "alignment-readback.json"), JsonSerializer.Serialize(new { before, edited, reopened, graphDifferences, metadataDifferences }));
            NativeImagePolicy.VerifyRestart(File.ReadAllBytes(output), edited, reopened);
            if (graphDifferences.Length != 0 || metadataDifferences.Length != 0) throw new InvalidDataException("Native alignment changed after restart; inspect alignment-readback.json. Original retained.");
            var receipt = edited.Alignment ?? throw new InvalidDataException("Native editor returned no alignment receipt.");
            var editorPolicy = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(editorDirectory, "native-layout-policy.json")));
            if (!editorPolicy.GetProperty("installed").GetBoolean() || !receipt.NoOp && !editorPolicy.GetProperty("acknowledged").GetBoolean())
                throw new InvalidDataException("Actual native editor policy/Task acknowledgment evidence is missing.");
            // Bind the structured receipt to the retained actual CEF callback, then derive
            // route intent independently in the modern host rather than trusting output geometry.
            var callbacks = File.ReadLines(Path.Combine(editorDirectory, "actual-cef-callbacks.jsonl"))
                .Select(line => JsonSerializer.Deserialize<JsonElement>(line)).Where(e => e.GetProperty("method").GetString() == "UpdateElementShape").ToArray();
            if (callbacks.Length != (receipt.NoOp ? 0 : 1)) throw new InvalidDataException("Native callback evidence count does not match the layout transaction.");
            if (!receipt.NoOp)
            {
                string payload = callbacks[0].GetProperty("payload").GetString()!;
                if (BpmnDocument.Revision(System.Text.Encoding.UTF8.GetBytes(payload)) != receipt.CallbackSha256)
                    throw new InvalidDataException("Native layout callback receipt hash mismatch.");
                var intent = NativeAlignmentPolicy.CallbackIntent(before.Elements, captured, payload);
                if (JsonSerializer.Serialize(intent) != JsonSerializer.Serialize(receipt.Changes)) throw new InvalidDataException("Native layout receipt differs from independent callback intent.");
            }
            var fidelity = NativeAlignmentPolicy.Compare(input.Bytes, File.ReadAllBytes(output), before.Elements, reopened.Elements, captured, receipt);
            File.WriteAllText(Path.Combine(directory, "alignment-fidelity.json"), JsonSerializer.Serialize(fidelity, new JsonSerializerOptions { WriteIndented = true }));
            if (!fidelity.Preserved) throw new InvalidDataException("Alignment changed unrequested native content; output is quarantined and the original is retained.");
            return new { before, edited, reopened, fidelity, receipt, editorPolicy, nativeSourceUnmodified = true,
                interpretationWarning = "Selected native alignment/distribution is not global auto-layout, live-document editing or desktop visual compatibility. Nonrepresentable label changes and semantic relocation are rejected.",
                outputArtifact = "artifact:" + id + ":edited.bpm", outputRevision = BpmnDocument.Revision(File.ReadAllBytes(output)) };
        });
    }
}
