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
    private static byte[] Archive(string body, string extra = "", string binary = "keep")
    {
        using var bytes = new MemoryStream();
        using (var outer = new ZipArchive(bytes, ZipArchiveMode.Create, true))
        {
            using (var writer = new StreamWriter(outer.CreateEntry("ModelInfo.xml").Open())) writer.Write("<ModelInfo/>");
            using (var writer = new StreamWriter(outer.CreateEntry("attachment.bin").Open())) writer.Write(binary);
            using var inner = new MemoryStream();
            using (var zip = new ZipArchive(inner, ZipArchiveMode.Create, true))
            { using var writer = new StreamWriter(zip.CreateEntry("Diagram.xml").Open()); writer.Write($"<Package xmlns='{Ns}'><WorkflowProcesses><WorkflowProcess Id='p'><Activities><Activity Id='{Id}' Name='Keep'>{body}</Activity></Activities></WorkflowProcess></WorkflowProcesses>{extra}</Package>"); }
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
}
