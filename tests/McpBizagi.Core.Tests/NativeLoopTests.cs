using System.Xml.Linq;
using McpBizagi.Contracts;
using McpBizagi.Core;
using Xunit;

namespace McpBizagi.Core.Tests;

/// <summary>Isolated loop intent and preservation policies. XML fixtures do not accredit the native engine.</summary>
public sealed class NativeLoopTests
{
    private static readonly XNamespace Ns = "http://www.wfmc.org/2009/XPDL2.2";
    private static XElement Owner(string xml) => XDocument.Parse($"<Package xmlns='{Ns}'><WorkflowProcesses><WorkflowProcess Id='p'><Activities><Activity Id='a'>{xml}<Unknown keep='true'/></Activity></Activities></WorkflowProcess></WorkflowProcesses></Package>").Descendants(Ns + "Activity").Single();
    private static NativeActivityLoop Standard() => new() { Kind = "Standard", Standard = new() { Maximum = 3, Counter = 2, TestBefore = true, Condition = "x < 4 & Ω" } };
    private const string StandardXml = "<Loop LoopType='Standard'><LoopStandard LoopMaximum='3' LoopCounter='2' TestTime='Before'><LoopCondition>x &lt; 4 &amp; Ω</LoopCondition></LoopStandard></Loop>";

    [Fact] public void CompleteStandardConfigurationIsAccepted() => NativeLoopPolicy.Validate(Standard());
    [Theory] [InlineData("All")] [InlineData("One")] [InlineData("None")] [InlineData("Complex")]
    public void ExactMultiInstanceBehaviors(string behavior) => NativeLoopPolicy.Validate(new() { Kind = "MultiInstance", MultiInstance = new() { Behavior = behavior } });
    [Theory] [InlineData("Invalid")] [InlineData("standard")]
    public void UnknownLoopKindIsRejected(string kind) => Assert.Throws<InvalidDataException>(() => NativeLoopPolicy.Validate(new() { Kind = kind }));
    [Theory] [InlineData("None")] [InlineData("MultiInstance")]
    public void NonmatchingConfigurationCannotBeIgnored(string kind) => Assert.Throws<InvalidDataException>(() => NativeLoopPolicy.Validate(new() { Kind = kind, Standard = new() }));
    [Theory] [InlineData("Standard")] [InlineData("MultiInstance")]
    public void MissingKindConfigurationIsRejected(string kind) => Assert.Throws<InvalidDataException>(() => NativeLoopPolicy.Validate(new() { Kind = kind }));
    [Fact] public void NegativeNativeCountersAreRejected() => Assert.Throws<InvalidDataException>(() => NativeLoopPolicy.Validate(new() { Kind = "Standard", Standard = new() { Counter = -1 } }));
    [Theory] [InlineData("All")] [InlineData("One")] [InlineData("None")]
    public void NoncomplexBehaviorCannotDiscardComplexCondition(string behavior) => Assert.Throws<InvalidDataException>(() => NativeLoopPolicy.Validate(new() { Kind = "MultiInstance", MultiInstance = new() { Behavior = behavior, ComplexCondition = "x" } }));
    [Fact] public void ReadbackCannotLoseTheLoop() => Assert.Throws<InvalidDataException>(() => NativeLoopPolicy.Verify(Standard(), new()));
    [Fact] public void ExactLoopReplacementLeavesUnrelatedXmlCompared()
    {
        var a = Owner("<Loop LoopType='None'/>"); var b = Owner(StandardXml);
        NativeLoopPolicy.Project(a, b, Standard()); Assert.True(XNode.DeepEquals(a, b));
        b.Element(Ns + "Unknown")!.Remove(); Assert.False(XNode.DeepEquals(a, b));
    }
    [Fact] public void ClearingStandardLoopRestoresOnlyComparisonCopy()
    {
        var a = Owner(StandardXml); var b = Owner("<Loop LoopType='None'/>");
        NativeLoopPolicy.Project(a, b, new()); Assert.True(XNode.DeepEquals(a, b));
    }
    [Fact] public void MultiInstanceDefaultsAreNativeSerializationDefaults()
    {
        var a = Owner("<Loop LoopType='None'/>"); var b = Owner("<Loop LoopType='MultiInstance'><LoopMultiInstance LoopCounter='0'/></Loop>");
        NativeLoopPolicy.Project(a, b, new() { Kind = "MultiInstance", MultiInstance = new() }); Assert.True(XNode.DeepEquals(a, b));
    }
    [Fact] public void SequentialComplexLoopHasTwoIndependentExpressions()
    {
        var a = Owner(StandardXml); var b = Owner("<Loop LoopType='MultiInstance'><LoopMultiInstance LoopCounter='4' MI_Ordering='Sequential' MI_FlowCondition='Complex'><MI_Condition>done</MI_Condition><ComplexMI_FlowCondition>complex</ComplexMI_FlowCondition></LoopMultiInstance></Loop>");
        NativeLoopPolicy.Project(a, b, new() { Kind = "MultiInstance", MultiInstance = new() { Counter = 4, IsSequential = true, Behavior = "Complex", CompletionCondition = "done", ComplexCondition = "complex" } }); Assert.True(XNode.DeepEquals(a, b));
    }
    [Theory] [InlineData("<Unknown/>")] [InlineData("<!--keep-->")] [InlineData("<![CDATA[  ]]>")] [InlineData("text")]
    public void UnknownLoopChildrenCannotBeRemoved(string content) => Assert.Throws<InvalidDataException>(() => NativeLoopPolicy.Project(Owner($"<Loop LoopType='None'>{content}</Loop>"), Owner(StandardXml), Standard()));
    [Theory] [InlineData("Extra='keep'")] [InlineData("xml:space='preserve'")] [InlineData("xmlns:custom='urn:custom'")]
    public void UnknownLoopAttributesCannotBeRemoved(string attribute) => Assert.Throws<InvalidDataException>(() => NativeLoopPolicy.Project(Owner($"<Loop LoopType='None' {attribute}/>"), Owner(StandardXml), Standard()));
    [Theory] [InlineData("<LoopCondition language='custom'>old</LoopCondition>")] [InlineData("<LoopCondition><Extension/></LoopCondition>")]
    public void UnknownExpressionMetadataIsRejected(string expression) => Assert.Throws<InvalidDataException>(() => NativeLoopPolicy.Project(Owner($"<Loop LoopType='Standard'><LoopStandard LoopCounter='0' LoopMaximum='2'>{expression}</LoopStandard></Loop>"), Owner(StandardXml), Standard()));
    [Fact] public void WrongPersistedLoopValueFailsInsteadOfProjection() => Assert.Throws<InvalidDataException>(() => NativeLoopPolicy.Project(Owner("<Loop LoopType='None'/>"), Owner(StandardXml.Replace("LoopMaximum='3'", "LoopMaximum='5'")), Standard()));
    [Fact] public void DuplicateLoopsAreRejected() => Assert.Throws<InvalidDataException>(() => NativeLoopPolicy.Project(Owner("<Loop LoopType='None'/><Loop LoopType='None'/>"), Owner(StandardXml), Standard()));
    [Theory] [InlineData("delete")] [InlineData("reconnect")]
    public void OtherOperationsCannotConcealLoopChanges(string operation) => Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([new() { Operation = operation, ElementId = "11111111-1111-4111-8111-111111111111", ActivityLoop = Standard() }]));
    [Fact] public void NonactivityCreationCannotAcceptLoop() => Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([new() { Operation = "create", ElementId = "11111111-1111-4111-8111-111111111111", ParentId = "22222222-2222-4222-8222-222222222222", ElementType = "NoneStart", ActivityLoop = Standard() }]));
}
