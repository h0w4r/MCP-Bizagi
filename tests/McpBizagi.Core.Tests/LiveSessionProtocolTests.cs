using McpBizagi.Contracts;
using McpBizagi.BizagiAdapter;
using Xunit;

namespace McpBizagi.Core.Tests;

public sealed class LiveSessionProtocolTests
{
    private static LiveSessionRequest Request(string action = "read") => new()
    { SessionId = Guid.NewGuid().ToString("D"), OperationId = Guid.NewGuid().ToString("D"), Action = action, ExpectedRevision = action == "read" ? "" : "observed-revision" };

    [Theory]
    [InlineData("read")][InlineData("undo")][InlineData("redo")]
    public void ExplicitActionsHaveValidEnvelopes(string action) => LiveSessionProtocol.Validate(Request(action));

    [Theory]
    [InlineData("save")][InlineData("execute")][InlineData("Invoke")][InlineData("")][InlineData("UPDATE")]
    public void UnimplementedOrArbitraryCommandsAreRejected(string action) => Assert.Throws<NotSupportedException>(() => LiveSessionProtocol.Validate(Request(action)));

    [Theory]
    [InlineData("update")][InlineData("undo")][InlineData("redo")][InlineData("checkpoint")][InlineData("close")]
    public void ChangesRequireAnObservedRevision(string action)
    {
        var request = Request(action); request.ExpectedRevision = "";
        Assert.Throws<ArgumentException>(() => LiveSessionProtocol.Validate(request));
    }

    [Fact]
    public void ExplicitEmptyValuesAndUnicodeArePreserved()
    {
        var request = Request("update"); request.Changes = [new() { ElementId = Guid.NewGuid().ToString("D"), Name = "Prueba Ω 日本語", Documentation = "" }];
        LiveSessionProtocol.Validate(request);
        Assert.Equal("", request.Changes[0].Documentation);
    }

    [Fact]
    public void DuplicateIdentityCannotHideTwoPatches()
    {
        string id = Guid.NewGuid().ToString("D");
        var request = Request("update"); request.Changes = [new() { ElementId = id, Name = "one" }, new() { ElementId = id.ToUpperInvariant(), Name = "two" }];
        Assert.Throws<ArgumentException>(() => LiveSessionProtocol.Validate(request));
    }

    [Fact]
    public void InvalidXmlIsRejectedBeforeNativeDispatch()
    {
        var request = Request("update"); request.Changes = [new() { ElementId = Guid.NewGuid().ToString("D"), Name = "bad\u0001" }];
        Assert.Throws<System.Xml.XmlException>(() => LiveSessionProtocol.Validate(request));
    }

    [Fact]
    public void ReadCannotCarryAHiddenMutation()
    {
        var request = Request(); request.Changes = [new() { ElementId = Guid.NewGuid().ToString("D"), Name = "hidden" }];
        Assert.Throws<ArgumentException>(() => LiveSessionProtocol.Validate(request));
    }

    [Fact]
    public void MissingPatchNullBatchAndEmptyUpdateAreRejected()
    {
        var request = Request("update"); Assert.Throws<ArgumentException>(() => LiveSessionProtocol.Validate(request));
        request.Changes = null!; Assert.Throws<ArgumentException>(() => LiveSessionProtocol.Validate(request));
        request.Changes = [null!]; Assert.Throws<ArgumentException>(() => LiveSessionProtocol.Validate(request));
        request.Changes = [new() { ElementId = Guid.NewGuid().ToString("D") }]; Assert.Throws<ArgumentException>(() => LiveSessionProtocol.Validate(request));
    }

    [Fact]
    public void OversizedBatchAndPropertiesAreRejected()
    {
        var request = Request("update");
        request.Changes = Enumerable.Range(0, 129).Select(_ => new LiveElementPatch { ElementId = Guid.NewGuid().ToString("D"), Name = "x" }).ToArray();
        Assert.Throws<ArgumentException>(() => LiveSessionProtocol.Validate(request));
        request.Changes = [new() { ElementId = Guid.NewGuid().ToString("D"), Name = new string('x', 16385) }];
        Assert.Throws<ArgumentException>(() => LiveSessionProtocol.Validate(request));
    }

    [Theory]
    [InlineData("")][InlineData("00000000-0000-0000-0000-000000000000")][InlineData("not-an-id")]
    public void InvalidSessionAndOperationIdsAreRejected(string value)
    {
        var request = Request(); request.SessionId = value; Assert.Throws<ArgumentException>(() => LiveSessionProtocol.Validate(request));
        request = Request(); request.OperationId = value; Assert.Throws<ArgumentException>(() => LiveSessionProtocol.Validate(request));
    }

    [Fact]
    public void UnknownProtocolIsNotSilentlyAccepted()
    { var request = Request(); request.ProtocolVersion = 2; Assert.Throws<NotSupportedException>(() => LiveSessionProtocol.Validate(request)); }

    [Fact]
    public void CheckpointRequiresBothLiveAndDiskRevisions()
    {
        var request = Request("checkpoint");
        Assert.Throws<ArgumentException>(() => LiveSessionProtocol.Validate(request));
        request.ExpectedDiskRevision = new string('a', 64);
        LiveSessionProtocol.Validate(request);
        request.ExpectedRevision = "";
        Assert.Throws<ArgumentException>(() => LiveSessionProtocol.Validate(request));
    }

    [Fact]
    public void CloseRequiresRetainedCheckpointIdentityAndBothRevisions()
    {
        var request = Request("close"); request.ExpectedDiskRevision = new string('a', 64);
        Assert.Throws<ArgumentException>(() => LiveSessionProtocol.Validate(request));
        request.CheckpointOperationId = Guid.NewGuid().ToString("D"); LiveSessionProtocol.Validate(request);
        request.ExpectedDiskRevision = ""; Assert.Throws<ArgumentException>(() => LiveSessionProtocol.Validate(request));
    }

    [Fact]
    public void OtherActionsCannotSmuggleClosePreconditions()
    {
        var request = Request(); request.CheckpointOperationId = Guid.NewGuid().ToString("D");
        Assert.Throws<ArgumentException>(() => LiveSessionProtocol.Validate(request));
    }

    [Theory]
    [InlineData("ABCDEF")][InlineData("not-a-revision")][InlineData(null)]
    public void CheckpointRejectsMalformedDiskRevisions(string? revision)
    {
        var request = Request("checkpoint"); request.ExpectedDiskRevision = revision!;
        Assert.Throws<ArgumentException>(() => LiveSessionProtocol.Validate(request));
    }

    [Fact]
    public void DiskPreconditionsCannotBeSilentlyIgnoredByOtherActions()
    {
        var request = Request(); request.ExpectedDiskRevision = new string('a', 64);
        Assert.Throws<ArgumentException>(() => LiveSessionProtocol.Validate(request));
    }

    [Fact]
    public void BatchSizeLimitAlsoBoundsAggregateDocumentText()
    {
        var request = Request("update");
        request.Changes = Enumerable.Range(0, 5).Select(_ => new LiveElementPatch { ElementId = Guid.NewGuid().ToString("D"), Documentation = new string('x', 1048576) }).ToArray();
        Assert.Throws<ArgumentException>(() => LiveSessionProtocol.Validate(request));
    }

    [Theory]
    [InlineData("McpBizagi.Worker")][InlineData("McpBizagi.LiveHost")]
    public void OwnedExecutableRolesHaveSeparatePreferenceNamespaces(string product)
        => Assert.Equal(product, NativeSettingsIdentity.RequireProduct("h0w4r", product));

    [Theory]
    [InlineData("Bizagi", "Bizagi Modeler")][InlineData("h0w4r", "Bizagi Modeler")]
    [InlineData("another", "McpBizagi.Worker")][InlineData("h0w4r", "../escape")][InlineData(null, null)]
    public void OperatorOrUnrecognizedIdentitiesCannotUseTheAdapter(string? company, string? product)
        => Assert.Throws<InvalidOperationException>(() => NativeSettingsIdentity.RequireProduct(company, product));
}
