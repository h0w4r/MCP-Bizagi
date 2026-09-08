using System.IO.Compression;
using McpBizagi.Contracts;
using McpBizagi.Core;
using Xunit;

namespace McpBizagi.Core.Tests;

/// <summary>Policy fixtures only; the native acceptance client independently exercises installed commands.</summary>
public sealed class NativeEventConversionTests
{
    private const string Id = "11111111-1111-4111-8111-111111111111";
    private const string MessageId = "22222222-2222-4222-8222-222222222222";
    private static NativeTypeConversion Change(string from = "TimerStart", string to = "SignalStart", string? mode = "Start") => new() { ElementId = Id, ExpectedType = from, TargetType = to, ExpectedEventMode = mode };
    private static byte[] Archive(string body, string extra = "", string runtime = "")
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            using (var writer = new StreamWriter(zip.CreateEntry("ModelInfo.xml").Open())) writer.Write("<ModelInfo/>");
            using var nested = new MemoryStream();
            using (var inner = new ZipArchive(nested, ZipArchiveMode.Create, true))
            {
                using var writer = new StreamWriter(inner.CreateEntry("Diagram.xml").Open());
                writer.Write($"<Package xmlns='http://www.wfmc.org/2009/XPDL2.2'><WorkflowProcesses><WorkflowProcess Id='p'><Activities><Activity Id='{Id}' Name='Keep'><Event>{body}</Event><Unknown>keep</Unknown><ExtendedAttributes>{runtime}</ExtendedAttributes></Activity></Activities>{extra}</WorkflowProcess></WorkflowProcesses></Package>");
            }
            using var target = zip.CreateEntry("test.diag").Open(); target.Write(nested.ToArray());
        }
        return output.ToArray();
    }

    [Theory]
    [InlineData("TimerStart", "SignalStart", null)]
    [InlineData("MessageIntermediate", "SignalIntermediate", "Unknown")]
    [InlineData("TimerIntermediate", "NoneIntermediate", "Catch")]
    [InlineData("UserTask", "ServiceTask", "Start")]
    [InlineData("NoneStart", "NoneEnd", "Start")]
    public void ExplicitSameRoleRequired(string source, string target, string? mode)
    { Assert.Throws<InvalidDataException>(() => NativeConversionPolicy.Validate([Change(source, target, mode)])); }

    [Fact] public void EveryDeclaredDistinctSameRolePairHasAnExplicitContract()
    {
        int pairs = 0;
        foreach (string mode in NativeEventConversionPolicy.Modes)
            foreach (string source in NativeEventConversionPolicy.TypesFor(mode))
                foreach (string target in NativeEventConversionPolicy.TypesFor(mode).Where(t => t != source))
                { NativeConversionPolicy.Validate([Change(source, target, mode)]); pairs++; }
        Assert.Equal(336, pairs);
    }

    [Fact] public void NeutralPayloadConversionPreservesAllOtherContent()
    {
        byte[] source = Archive("<StartEvent Trigger='Timer'><TriggerTimer/></StartEvent>");
        byte[] target = Archive("<StartEvent Trigger='Signal'><TriggerResultSignal/></StartEvent>");
        Assert.True(NativeConversionPolicy.Compare(source, target, [Change()]).Preserved);
        Assert.False(NativeConversionPolicy.Compare(source, Archive("<StartEvent Trigger='Signal' Interrupting='false'><TriggerResultSignal/></StartEvent>"), [Change()]).Preserved);
    }

    [Theory]
    [InlineData("<TriggerTimer TimeCycle='R1/PT1M'/>")]
    [InlineData("<TriggerTimer Unknown='keep'/>")]
    [InlineData("<TriggerTimer><!--keep--></TriggerTimer>")]
    [InlineData("<TriggerTimer xml:space='preserve'/>")]
    [InlineData("<TriggerTimer/><Unknown/>")]
    public void ConfiguredOrUnknownEventPayloadIsNotRetired(string payload)
    { Assert.Throws<InvalidDataException>(() => NativeConversionPolicy.Preflight(Archive("<StartEvent Trigger='Timer'>" + payload + "</StartEvent>"), [Change()])); }

    [Fact] public void OnlyAnUnreferencedEmptyMessageIdentityMayBeRetired()
    {
        string message = $"<StartEvent Trigger='Message'><TriggerResultMessage><Message Id='{MessageId}'/></TriggerResultMessage></StartEvent>";
        var change = Change("MessageStart");
        Assert.True(NativeConversionPolicy.Compare(Archive(message), Archive("<StartEvent Trigger='Signal'><TriggerResultSignal/></StartEvent>"), [change]).Preserved);
        Assert.Throws<InvalidDataException>(() => NativeConversionPolicy.Preflight(Archive(message, $"<Unknown Ref='{MessageId}'/>"), [change]));
        Assert.Throws<InvalidDataException>(() => NativeConversionPolicy.Preflight(Archive(message.Replace("<Message Id=", "<Message Name='configured' Id=")), [change]));
    }

    [Fact] public void BoundaryAttachmentAndInterruptionRemainCompared()
    {
        string a = $"<IntermediateEvent Trigger='Timer' Target='{MessageId}' IsAttached='true' Interrupting='false'><TriggerTimer/></IntermediateEvent>";
        string b = a.Replace("Timer", "Signal").Replace("TriggerSignal", "TriggerResultSignal");
        var change = Change("TimerIntermediate", "SignalIntermediate", "Boundary");
        Assert.True(NativeConversionPolicy.Compare(Archive(a), Archive(b), [change]).Preserved);
        Assert.False(NativeConversionPolicy.Compare(Archive(a), Archive(b.Replace(MessageId, Id)), [change]).Preserved);
        Assert.Throws<InvalidDataException>(() => NativeConversionPolicy.Preflight(Archive(a), [Change("TimerIntermediate", "SignalIntermediate", "Catch")]));
    }

    [Fact] public void NativeEndCollectionOrderIsIndependentOfXpdlPayloadOrder()
    {
        var before = new NativeElement { Id = Id, Kind = "EndEvent", ElementType = "MultipleEnd", Event = new()
        {
            Mode = "End", DefinitionKinds = ["Message", "Error", "Compensation", "Signal"], Definitions =
            [new() { Kind = "Message", Name = "" }, new() { Kind = "Error", ErrorCode = "" },
             new() { Kind = "Compensation", Compensation = new() { WaitForCompletion = false } }, new() { Kind = "Signal", Name = "" }]
        } };
        var after = new NativeElement { Id = Id, Kind = "EndEvent", ElementType = "NoneEnd", Event = new() { Mode = "End" } };
        var change = Change("MultipleEnd", "NoneEnd", "End");
        NativeConversionPolicy.Verify([before], [after], [change]);
        before.Event.Definitions[2].Compensation!.ActivityId = MessageId;
        Assert.Throws<InvalidDataException>(() => NativeConversionPolicy.Verify([before], [after], [change]));
        // Serializer order is intentionally different from the in-memory order.
        string xml = $"<EndEvent Result='Multiple'><ResultMultiple><TriggerResultMessage CatchThrow='THROW'><Message Id='{MessageId}'/></TriggerResultMessage><TriggerResultCompensation WaitForCompletion='false'/><ResultError/><TriggerResultSignal CatchThrow='THROW'/></ResultMultiple></EndEvent>";
        Assert.True(NativeConversionPolicy.Compare(Archive(xml), Archive("<EndEvent Result='None'/>"), [change]).Preserved);
    }

    [Theory]
    [InlineData("{&quot;cost&quot;:0}", true)]
    [InlineData("{&quot;cost&quot;:1}", false)]
    [InlineData("{&quot;unknown&quot;:0}", false)]
    [InlineData("{&quot;priority&quot;:0}", false)]
    [InlineData("{&quot;cost&quot;:&quot;0&quot;}", false)]
    public void OnlyObservedTypeSpecificRuntimeDefaultsAreNeutral(string json, bool accepted)
    {
        string runtime = "<ExtendedAttribute Name='RuntimeProperties' Value='" + json + "'/>";
        Assert.Equal(accepted, NativeConversionPolicy.Compare(Archive("<StartEvent Trigger='Timer'><TriggerTimer/></StartEvent>", runtime: runtime),
            Archive("<StartEvent Trigger='Signal'><TriggerResultSignal/></StartEvent>"), [Change()]).Preserved);
    }
}
