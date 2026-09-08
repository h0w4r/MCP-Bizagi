using System.IO.Compression;
using System.Text.Json;
using McpBizagi.Contracts;
using McpBizagi.Core;
using Xunit;

namespace McpBizagi.Core.Tests;

/// <summary>Exact conversion policy checks, separate from native command execution accreditation.</summary>
public sealed class NativeConversionTests
{
    private const string Id = "11111111-1111-4111-8111-111111111111";
    private const string Ns = "http://www.wfmc.org/2009/XPDL2.2";
    private static NativeTypeConversion Change(string source = "ScriptTask", string target = "ManualTask") => new() { ElementId = Id, ExpectedType = source, TargetType = target };
    private static byte[] Archive(string body, string extra = "", string binary = "keep", string values = "", Dictionary<string, string>? definitions = null)
    {
        using var bytes = new MemoryStream();
        using (var outer = new ZipArchive(bytes, ZipArchiveMode.Create, true))
        {
            using (var writer = new StreamWriter(outer.CreateEntry("ModelInfo.xml").Open())) writer.Write("<ModelInfo/>");
            using (var writer = new StreamWriter(outer.CreateEntry("attachment.bin").Open())) writer.Write(binary);
            foreach (var definition in definitions ?? [])
            { using var writer = new StreamWriter(outer.CreateEntry("Documentation/" + definition.Key + ".xml").Open()); writer.Write(definition.Value); }
            using var inner = new MemoryStream();
            using (var zip = new ZipArchive(inner, ZipArchiveMode.Create, true))
            {
                using (var writer = new StreamWriter(zip.CreateEntry("Diagram.xml").Open())) writer.Write($"<Package xmlns='{Ns}'><WorkflowProcesses><WorkflowProcess Id='p'><Activities><Activity Id='{Id}' Name='Keep'>{body}</Activity></Activities></WorkflowProcess></WorkflowProcesses>{extra}</Package>");
                if (values != "") { using var writer = new StreamWriter(zip.CreateEntry("ExtendedAttributeValues.xml").Open()); writer.Write(values); }
            }
            using var target = outer.CreateEntry("test.diag").Open(); target.Write(inner.ToArray());
        }
        return bytes.ToArray();
    }
    private static string Task(string marker) => "<Implementation><Task>" + marker + "</Task></Implementation>";
    [Fact] public void ExactEmptyScriptFactoryExpressionCanConvert()
    { Assert.True(NativeConversionPolicy.Compare(Archive(Task("<TaskScript><Script/></TaskScript>")), Archive(Task("<TaskManual/>")), [Change()]).Preserved); }
    [Fact] public void ProjectedEmptyWrappersDoNotAcquireIndentationData()
    { Assert.True(NativeConversionPolicy.Compare(Archive(Task("\n <TaskScript><Script/></TaskScript>\n")), Archive(Task("")), [Change("ScriptTask", "AbstractTask")]).Preserved); }
    [Fact] public void PreservedWhitespaceAndCommentsAreNotFormattingWaivers()
    {
        Assert.Throws<InvalidDataException>(() => NativeConversionPolicy.Preflight(Archive("<Implementation xml:space='preserve'><Task> <TaskManual/> </Task></Implementation>"), [Change("ManualTask", "UserTask")]));
        Assert.False(NativeConversionPolicy.Compare(Archive(Task("<!--keep--><TaskManual/>")), Archive(Task("<TaskUser/>")), [Change("ManualTask", "UserTask")]).Preserved);
    }
    [Theory] [InlineData("{}", true)] [InlineData("{&quot;isAsynchronous&quot;:false}", true)] [InlineData("{&quot;isAsynchronous&quot;:true}", false)] [InlineData("{&quot;unknown&quot;:false}", false)]
    public void ServiceAsynchronousNeutralDefaultIsExact(string behavior, bool expected)
    {
        string runtime = "<ExtendedAttributes>\n<ExtendedAttribute Name='RuntimeProperties' Value='{&quot;asynchronousBehavior&quot;:" + behavior + "}'/>\n</ExtendedAttributes>";
        Assert.Equal(expected, NativeConversionPolicy.Compare(Archive(Task("<TaskService/>") + runtime), Archive(Task("") + "<ExtendedAttributes/>"), [Change("ServiceTask", "AbstractTask")]).Preserved);
    }
    [Theory]
    [InlineData("<Script> </Script>")]
    [InlineData("<Script>text</Script>")]
    [InlineData("<Script language='unknown'/>")]
    [InlineData("<Script><!--keep--></Script>")]
    [InlineData("<Script><Unknown/></Script>")]
    [InlineData("<Script/><Script/>")]
    [InlineData("<x:Script xmlns:x='urn:unknown'/>")]
    public void ScriptContentCannotBeDiscarded(string script)
    { Assert.Throws<InvalidDataException>(() => NativeConversionPolicy.Preflight(Archive(Task("<TaskScript>" + script + "</TaskScript>")), [Change()])); }
    [Theory]
    [InlineData("<TaskManual Instantiate='false'/>", "ManualTask")]
    [InlineData("<TaskUser Implementation='WebService'/>", "UserTask")]
    [InlineData("<TaskManual xmlns:q='urn:unknown'/>", "ManualTask")]
    public void WrongTypeDefaultsAndUnknownAttributesFail(string marker, string kind)
    { Assert.Throws<InvalidDataException>(() => NativeConversionPolicy.Preflight(Archive(Task(marker)), [Change(kind, "ScriptTask")])); }
    [Theory]
    [InlineData("<TaskUser Implementation='Unspecified'/>", "UserTask")]
    [InlineData("<TaskReceive Instantiate='false'/>", "ReceiveTask")]
    [InlineData("<TaskBusinessRule BusinessRuleTaskImplementation='Unspecified'/>", "BusinessRuleTask")]
    [InlineData("<TaskService/>", "ServiceTask")]
    public void ExactKnownTaskDefaultsCanConvert(string marker, string kind)
    { Assert.True(NativeConversionPolicy.Compare(Archive(Task(marker)), Archive(Task("<TaskManual/>")), [Change(kind, "ManualTask")]).Preserved); }
    [Fact] public void ImplicitExclusiveGatewaySelectorIsRecognized()
    { Assert.True(NativeConversionPolicy.Compare(Archive("<Route/>"), Archive("<Route GatewayType='Parallel'/>"), [Change("ExclusiveGateway", "ParallelGateway")]).Preserved); }
    [Fact] public void GatewayUnknownFieldsRemainCompared()
    { Assert.False(NativeConversionPolicy.Compare(Archive("<Route Custom='keep'/>"), Archive("<Route GatewayType='Parallel'/>"), [Change("ExclusiveGateway", "ParallelGateway")]).Preserved); }
    [Fact] public void ComplexConditionIsNotSilentlyRetired()
    { Assert.Throws<InvalidDataException>(() => NativeConversionPolicy.Preflight(Archive("<Route GatewayType='Complex' IncomingCondition='keep'/>"), [Change("ComplexGateway", "ParallelGateway")])); }
    [Fact] public void UnknownArchiveContentAndBytesAreProtected()
    {
        var source = Archive(Task("<TaskScript><Script/></TaskScript>"), "<Unknown>keep</Unknown>");
        Assert.False(NativeConversionPolicy.Compare(source, Archive(Task("<TaskManual/>"), "<Unknown>changed</Unknown>"), [Change()]).Preserved);
        Assert.False(NativeConversionPolicy.Compare(source, Archive(Task("<TaskManual/>"), "<Unknown>keep</Unknown>", "changed"), [Change()]).Preserved);
    }
    [Fact] public void ExactNeutralRuntimeDefaultsAreNotUnknownData()
    {
        string defaults = "<ExtendedAttributes><ExtendedAttribute Name='RuntimeProperties' Value='{&quot;cost&quot;:0,&quot;priority&quot;:0,&quot;notifyOnMobile&quot;:false,&quot;isSingleton&quot;:false,&quot;isConditional&quot;:false}'/></ExtendedAttributes>";
        Assert.True(NativeConversionPolicy.Compare(Archive(Task("<TaskUser/>") + defaults), Archive(Task("") + "<ExtendedAttributes/>"), [Change("UserTask", "AbstractTask")]).Preserved);
        Assert.False(NativeConversionPolicy.Compare(Archive(Task("<TaskUser/>") + defaults.Replace("&quot;cost&quot;:0", "&quot;cost&quot;:5")), Archive(Task("") + "<ExtendedAttributes/>"), [Change("UserTask", "AbstractTask")]).Preserved);
        Assert.False(NativeConversionPolicy.Compare(Archive(Task("<TaskUser/>") + defaults.Replace("&quot;cost&quot;:0", "&quot;unknown&quot;:0")), Archive(Task("") + "<ExtendedAttributes/>"), [Change("UserTask", "AbstractTask")]).Preserved);
    }
    [Fact] public void DuplicateEmptyAndCrossCategoryRequestsFail()
    {
        Assert.Throws<InvalidDataException>(() => NativeConversionPolicy.Validate([]));
        Assert.Throws<InvalidDataException>(() => NativeConversionPolicy.Validate([Change(), Change()]));
        Assert.Throws<NotSupportedException>(() => NativeConversionPolicy.Validate([Change("UserTask", "ParallelGateway")]));
    }
    [Fact] public void CommonGraphPropertiesAndUnconvertedElementsStayExact()
    {
        NativeElement Before() => new() { Id = Id, Kind = "UserTask", ElementType = "UserTask", Name = "Keep", ParentId = "p", DiagramId = "d", Style = new() { FontSize = 12 } };
        var a = Before(); var b = Before(); b.Kind = "ManualTask"; b.ElementType = "ManualTask";
        NativeConversionPolicy.Verify([a], [b], [Change("UserTask", "ManualTask")]);
        b.Style!.FontSize = 10;
        Assert.Throws<InvalidDataException>(() => NativeConversionPolicy.Verify([a], [b], [Change("UserTask", "ManualTask")]));
    }
    [Fact] public void PaletteSelectorCannotHideWrongNativeClass()
    {
        var a = new NativeElement { Id = Id, Kind = "Task", ElementType = "AbstractTask" };
        var b = new NativeElement { Id = Id, Kind = "ManualTask", ElementType = "ManualTask" };
        NativeConversionPolicy.Verify([a], [b], [Change("AbstractTask", "ManualTask")]);
        b.Kind = "SubProcess";
        Assert.Throws<InvalidDataException>(() => NativeConversionPolicy.Verify([a], [b], [Change("AbstractTask", "ManualTask")]));
    }

    [Theory]
    [InlineData("AbstractTask", "")]
    [InlineData("UserTask", "<TaskUser Implementation='Unspecified' />")]
    [InlineData("ManualTask", "<TaskManual />")]
    [InlineData("ServiceTask", "<TaskService />")]
    [InlineData("ScriptTask", "<TaskScript><Script /></TaskScript>")]
    [InlineData("SendTask", "<TaskSend />")]
    [InlineData("ReceiveTask", "<TaskReceive Instantiate='false' />")]
    [InlineData("BusinessRuleTask", "<TaskBusinessRule BusinessRuleTaskImplementation='Unspecified' />")]
    public void TaskToUnboundCallChangesOnlyVerifiedSelector(string source, string selector)
    {
        var change = Change(source, "CallActivity");
        string common = "<Unknown>keep</Unknown><Loop><LoopStandard LoopMaximum='4'/></Loop>";
        Assert.True(NativeConversionPolicy.Compare(Archive(Task(selector) + common), Archive("<Implementation><SubFlow Id='' /></Implementation>" + common), [change]).Preserved);
        Assert.Throws<NotSupportedException>(() => NativeConversionPolicy.Validate([Change("CallActivity", source)]));
    }

    [Theory]
    [InlineData("Id='11111111-1111-4111-8111-111111111111'", "")]
    [InlineData("Id='' Extra='unknown'", "")]
    [InlineData("Id=''", "<!--keep-->")]
    [InlineData("Id=''", " ")]
    [InlineData("Id=''", "<Unknown/>")]
    public void TaskToCallCannotIntroduceBoundOrUnknownPayload(string attributes, string body)
    {
        Assert.Throws<InvalidDataException>(() => NativeConversionPolicy.Compare(Archive(Task("<TaskManual/>")),
            Archive($"<Implementation><SubFlow {attributes}>{body}</SubFlow></Implementation>"), [Change("ManualTask", "CallActivity")]));
    }

    [Fact] public void TaskToCallGraphRequiresEmptyReferenceAndPreservesCommonSemantics()
    {
        var source = new NativeElement { Id = Id, Kind = "UserTask", ElementType = "UserTask", Geometry = new() { X = 10, Y = 20, Width = 140, Height = 70 }, ActivityProperties = new() { StartQuantity = 2, CompletionQuantity = 3 } };
        var result = JsonSerializer.Deserialize<NativeElement>(JsonSerializer.Serialize(source))!;
        result.Kind = result.ElementType = "CallActivity"; result.CallReference = new(); result.ExpandedGeometry = new() { X = 10, Y = 20 };
        NativeConversionPolicy.Verify([source], [result], [Change("UserTask", "CallActivity")]);
        result.CallReference.CatalogProcessId = Id;
        Assert.Throws<InvalidDataException>(() => NativeConversionPolicy.Verify([source], [result], [Change("UserTask", "CallActivity")]));
        result.CallReference.CatalogProcessId = ""; result.ExpandedGeometry.X = 30;
        Assert.Throws<InvalidDataException>(() => NativeConversionPolicy.Verify([source], [result], [Change("UserTask", "CallActivity")]));
        result.ExpandedGeometry.X = 10; result.ActivityProperties!.StartQuantity = 1;
        Assert.Throws<InvalidDataException>(() => NativeConversionPolicy.Verify([source], [result], [Change("UserTask", "CallActivity")]));
    }

    [Fact] public void HiddenExpandedDimensionsAndUnknownCallContentRemainArchiveProtected()
    {
        const string graphics = "<NodeGraphicsInfos><NodeGraphicsInfo ExpandedWidth='450' ExpandedHeight='280'><Coordinates XCoordinate='10' YCoordinate='20'/></NodeGraphicsInfo></NodeGraphicsInfos>";
        var source = Archive(Task("<TaskManual/>") + graphics, binary: "attachment");
        var change = Change("ManualTask", "CallActivity");
        Assert.True(NativeConversionPolicy.Compare(source, Archive("<Implementation><SubFlow Id=''/></Implementation>" + graphics, binary: "attachment"), [change]).Preserved);
        Assert.False(NativeConversionPolicy.Compare(source, Archive("<Implementation><SubFlow Id=''/></Implementation>" + graphics.Replace("450", "0"), binary: "attachment"), [change]).Preserved);
        Assert.False(NativeConversionPolicy.Compare(source, Archive("<Implementation><SubFlow Id=''/></Implementation>" + graphics, binary: "changed"), [change]).Preserved);
    }

    [Theory]
    [InlineData("0", "AllTokens", "{}", true)]
    [InlineData("2", "AllTokens", "{}", false)]
    [InlineData("0", "OneToken", "{}", false)]
    [InlineData("0", "AllTokens", "{&quot;isAsynchronous&quot;:true}", false)]
    [InlineData("0", "AllTokens", "{&quot;unknown&quot;:false}", false)]
    public void OnlyExactObservedCallFactoryDefaultsAreProjected(string priority, string exit, string behavior, bool expected)
    {
        string runtime = "<ExtendedAttributes><ExtendedAttribute Name='RuntimeProperties' Value='{&quot;priority&quot;:" + priority +
            ",&quot;asynchronousBehavior&quot;:" + behavior + ",&quot;subProcessType&quot;:&quot;None&quot;,&quot;inputMappingType&quot;:&quot;None&quot;,&quot;outputMappingType&quot;:&quot;None&quot;,&quot;exitMode&quot;:&quot;" + exit + "&quot;}'/></ExtendedAttributes>";
        const string geometry = "<NodeGraphicsInfos><NodeGraphicsInfo Expanded='false' ExpandedWidth='0' ExpandedHeight='0'/></NodeGraphicsInfos>";
        Assert.Equal(expected, NativeConversionPolicy.Compare(Archive(Task("<TaskManual/>") + "<NodeGraphicsInfos><NodeGraphicsInfo/></NodeGraphicsInfos><ExtendedAttributes/>"),
            Archive("<Implementation><SubFlow Id=''/></Implementation>" + geometry + runtime), [Change("ManualTask", "CallActivity")]).Preserved);
    }

    [Theory] [InlineData("CallActivity")] [InlineData("ServiceTask")]
    public void ConversionRequiresAttributeScopeForTheExactDestination(string target)
    {
        const string definition = "22222222-2222-4222-8222-222222222222";
        string values = $"<DiagramAttributeValues><ElementAttributeValues ElementId='{Id}'><Values><ExtendedAttributeValue Id='{definition}' Type='Text'><Content>keep</Content></ExtendedAttributeValue></Values></ElementAttributeValues></DiagramAttributeValues>";
        string xml = $"<ExtendedAttribute Id='{definition}'><ElementTypes><AttributeElementType Type='UserTask'/></ElementTypes></ExtendedAttribute>";
        byte[] Source(string scope) => Archive(Task("<TaskUser/>"), values: values, definitions: new() { [definition] = xml.Replace("UserTask", scope) });
        Assert.Throws<InvalidDataException>(() => NativeConversionPolicy.Preflight(Source("UserTask"), [Change("UserTask", target)]));
        Assert.Throws<InvalidDataException>(() => NativeConversionPolicy.Preflight(Archive(Task("<TaskUser/>"), values: values), [Change("UserTask", target)]));
        NativeConversionPolicy.Preflight(Source(target), [Change("UserTask", target)]);
    }
}
