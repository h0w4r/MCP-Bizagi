using McpBizagi.Contracts;
using McpBizagi.Core;
using Xunit;

namespace McpBizagi.Core.Tests;

public sealed class NativeEditPlanTests
{
    // These deterministic DTO tests exercise the contract only, not the installed Bizagi engine.
    private const string ElementId = "abcdef12-3456-4789-abcd-123456789abc";
    private const string ParentId = "11111111-2222-4333-8444-555555555555";
    private const string SourceId = "22222222-3333-4444-8555-666666666666";
    private const string TargetId = "33333333-4444-4555-8666-777777777777";

    private static NativeGeometry Bounds() => new()
    {
        X = 20,
        Y = 30,
        Width = 100,
        Height = 60,
        BackgroundArgb = unchecked((int)0xff112233),
        BorderArgb = unchecked((int)0xff445566)
    };

    private static NativeMutation Update() => new() { Operation = "update", ElementId = ElementId, Name = "Review request" };
    private static NativeMutation Create(string type = "UserTask") => new()
    {
        Operation = "create",
        ElementId = ElementId,
        ParentId = ParentId,
        ElementType = type,
        Name = "Review request",
        Documentation = "Review the supplied request.",
        Geometry = Bounds()
    };
    private static NativeMutation Connector(string operation = "reconnect") => new()
    {
        Operation = operation,
        ElementId = ElementId,
        SourceId = SourceId,
        TargetId = TargetId,
        Points = [new() { X = 0, Y = 10 }, new() { X = 30, Y = 40 }]
    };

    [Fact]
    public void EmptyAndOversizedBatchesAreRejected()
    {
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([]));
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate(Enumerable.Range(1, 1001)
            .Select(i => new NativeMutation { Operation = "update", ElementId = new Guid(i, 0, 0, new byte[8]).ToString(), Name = "Name" }).ToArray()));
    }

    [Fact]
    public void BatchAtTheUpperLimitAcceptsDistinctCanonicalIdentities()
    {
        var batch = Enumerable.Range(1, 1000).Select(i => new NativeMutation
        { Operation = "update", ElementId = new Guid(i, 0, 0, new byte[8]).ToString(), Name = "Name" }).ToArray();
        NativeEditPlan.Validate(batch);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("ABCDEF12-3456-4789-ABCD-123456789ABC")]
    [InlineData("abcdef1234564789abcd123456789abc")]
    [InlineData("{abcdef12-3456-4789-abcd-123456789abc}")]
    [InlineData(" abcdef12-3456-4789-abcd-123456789abc ")]
    public void InvalidIdentityRepresentationsAreRejectedInEveryIdentityField(string value)
    {
        var mutation = Update(); mutation.ElementId = value;
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([mutation]));
        mutation = Create(); mutation.ParentId = value;
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([mutation]));
        mutation = Connector(); mutation.SourceId = value;
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([mutation]));
        mutation = Connector(); mutation.TargetId = value;
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([mutation]));
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("rename", "")]
    [InlineData("Update", "")]
    [InlineData("create", "UnknownType")]
    [InlineData("create", "usertask")]
    public void UnknownOperationsAndElementTypesAreRejected(string operation, string type)
    {
        var mutation = Create(type); mutation.Operation = operation;
        Assert.Throws<NotSupportedException>(() => NativeEditPlan.Validate([mutation]));
    }

    [Fact]
    public void EveryAllowlistedCreateTypeAcceptsTheCorrectFieldShape()
    {
        foreach (var type in NativeEditPlan.CreatableTypes)
        {
            var mutation = Create(type);
            if (type == "Participant") mutation.ProcessId = TargetId;
            if (type.EndsWith("Intermediate", StringComparison.Ordinal))
            {
                mutation.EventMode = type is "NoneIntermediate" or "EscalationIntermediate" or "CompensationIntermediate" ? "Throw" : "Catch";
                if (type is "ErrorIntermediate" or "CancelIntermediate")
                {
                    mutation.EventMode = "Boundary";
                    mutation.EventProperties = new() { AttachedToActivityId = TargetId };
                }
            }
            if (type is "SequenceFlow" or "MessageFlow")
            {
                mutation.Geometry = null; mutation.SourceId = SourceId; mutation.TargetId = TargetId;
                mutation.Points = [new() { X = 0, Y = 0 }, new() { X = 100, Y = 0 }];
            }
            NativeEditPlan.Validate([mutation]);
        }
    }

    [Fact]
    public void RepeatedIdentityIsRejectedEvenAcrossDifferentOperations()
    {
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([Update(), new() { Operation = "delete", ElementId = ElementId }]));
    }

    [Theory]
    [InlineData("update")]
    [InlineData("delete")]
    [InlineData("reconnect")]
    public void CreationOnlyFieldsAreRejectedOnOtherOperations(string operation)
    {
        var mutation = operation == "reconnect" ? Connector() : Update(); mutation.Operation = operation;
        mutation.ParentId = ParentId;
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([mutation]));
        mutation.ParentId = ""; mutation.ElementType = "UserTask";
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([mutation]));
    }

    [Theory]
    [InlineData("source")]
    [InlineData("target")]
    [InlineData("points")]
    public void ConnectorFieldsAreRejectedOnNodeUpdates(string field)
    {
        var mutation = Update();
        if (field == "source") mutation.SourceId = SourceId;
        if (field == "target") mutation.TargetId = TargetId;
        if (field == "points") mutation.Points = [new() { X = 1, Y = 2 }];
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([mutation]));
    }

    [Theory]
    [InlineData("delete", "name")]
    [InlineData("delete", "documentation")]
    [InlineData("delete", "geometry")]
    [InlineData("reconnect", "name")]
    [InlineData("reconnect", "documentation")]
    [InlineData("reconnect", "geometry")]
    public void DeleteAndReconnectRejectNodeProperties(string operation, string property)
    {
        var mutation = operation == "reconnect" ? Connector() : new NativeMutation { Operation = "delete", ElementId = ElementId };
        if (property == "name") mutation.Name = "";
        if (property == "documentation") mutation.Documentation = "";
        if (property == "geometry") mutation.Geometry = Bounds();
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([mutation]));
    }

    [Fact]
    public void UpdatesRequireAPropertyButCanExplicitlyClearText()
    {
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([new() { Operation = "update", ElementId = ElementId }]));
        NativeEditPlan.Validate([new() { Operation = "update", ElementId = ElementId, Name = "" }]);
        NativeEditPlan.Validate([new() { Operation = "update", ElementId = ElementId, Documentation = "" }]);
        NativeEditPlan.Validate([new() { Operation = "update", ElementId = ElementId, Geometry = Bounds() }]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10001)]
    public void ConnectorPointCountOutsideTheContractIsRejected(int count)
    {
        var mutation = Connector(); mutation.Points = Enumerable.Range(0, count).Select(i => new NativePoint { X = i, Y = 0 }).ToArray();
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([mutation]));
    }

    [Fact]
    public void ConnectorPointCountAtTheUpperLimitIsAccepted()
    {
        var mutation = Connector(); mutation.Points = Enumerable.Range(0, 10000).Select(i => new NativePoint { X = i, Y = 0 }).ToArray();
        NativeEditPlan.Validate([mutation]);
        mutation.Geometry = Bounds();
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([mutation]));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(1000001)]
    [InlineData(-1000001)]
    public void NonFiniteAndOutOfRangeCoordinatesAreRejectedEverywhere(double value)
    {
        foreach (var property in new[] { "X", "Y", "Width", "Height" })
        {
            var mutation = Create();
            // Each coordinate is independently varied while all other inputs remain valid.
            typeof(NativeGeometry).GetProperty(property)!.SetValue(mutation.Geometry, value);
            Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([mutation]));
        }
        var connection = Connector(); connection.Points[0].X = value;
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([connection]));
        connection = Connector(); connection.Points[1].Y = value;
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([connection]));
    }

    [Theory]
    [InlineData(0, 60)]
    [InlineData(-1, 60)]
    [InlineData(100, 0)]
    [InlineData(100, -1)]
    public void NonPositiveNodeSizesAreRejected(double width, double height)
    {
        var mutation = Create(); mutation.Geometry!.Width = width; mutation.Geometry.Height = height;
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([mutation]));
    }

    [Fact]
    public void CoordinateBoundariesAreAcceptedButExpandedGeometryIsRejected()
    {
        var mutation = Create(); mutation.Geometry = new() { X = -1000000, Y = 1000000, Width = 1000000, Height = 0.01 };
        NativeEditPlan.Validate([mutation]);
        mutation.Geometry.Expanded = true;
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([mutation]));
    }

    [Fact]
    public void TextLengthsAreBoundedWithoutRejectingUnicodeOrEmptyValues()
    {
        var mutation = Update(); mutation.Name = new string('é', 10000); mutation.Documentation = new string('界', 1024 * 1024);
        NativeEditPlan.Validate([mutation]);
        mutation.Name += "x";
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([mutation]));
        mutation.Name = ""; mutation.Documentation += "x";
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([mutation]));
    }

    private static NativeElement Readback() => new()
    {
        Id = ElementId,
        ParentId = ParentId,
        ElementType = "UserTask",
        Name = "Review request",
        Documentation = "Review the supplied request.",
        Geometry = Bounds()
    };

    [Fact]
    public void CreationRequiresExactlyOneIdentityAndExactTypeAndContainment()
    {
        var mutation = Create(); var element = Readback();
        NativeEditPlan.Verify([mutation], [element]);
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Verify([mutation], []));
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Verify([mutation], [element, element]));
        element.Id = SourceId;
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Verify([mutation], [element]));
        element = Readback(); element.ParentId = SourceId;
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Verify([mutation], [element]));
        element = Readback(); element.ElementType = "ManualTask";
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Verify([mutation], [element]));
    }

    [Fact]
    public void DeletionRequiresAbsenceButDoesNotRejectUnrelatedElements()
    {
        NativeMutation mutation = new() { Operation = "delete", ElementId = ElementId };
        NativeEditPlan.Verify([mutation], []);
        NativeEditPlan.Verify([mutation], [new() { Id = SourceId }]);
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Verify([mutation], [Readback()]));
    }

    [Fact]
    public void TextReadbackRequiresExactUnicodeContentIncludingExplicitClears()
    {
        var mutation = Update(); mutation.Name = "Revisión 東京"; mutation.Documentation = "First\nSecond";
        var element = Readback(); element.Name = mutation.Name; element.Documentation = mutation.Documentation;
        NativeEditPlan.Verify([mutation], [element]);
        element.Name += " ";
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Verify([mutation], [element]));
        element.Name = mutation.Name; element.Documentation = "First Second";
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Verify([mutation], [element]));
        mutation.Name = ""; mutation.Documentation = "";
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Verify([mutation], [element]));
        element.Name = ""; element.Documentation = "";
        NativeEditPlan.Verify([mutation], [element]);
    }

    [Theory]
    [InlineData("X")]
    [InlineData("Y")]
    [InlineData("Width")]
    [InlineData("Height")]
    public void GeometryReadbackRejectsChangesToEveryCoordinate(string property)
    {
        var mutation = Create(); var element = Readback();
        typeof(NativeGeometry).GetProperty(property)!.SetValue(element.Geometry, 999d);
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Verify([mutation], [element]));
    }

    [Fact]
    public void GeometryReadbackAllowsNativeFloatPrecisionButNotMissingGeometryOrExpansionChanges()
    {
        var mutation = Create(); mutation.Geometry!.X = 123456.789;
        var element = Readback(); element.Geometry!.X = (float)mutation.Geometry.X;
        NativeEditPlan.Verify([mutation], [element]);
        element.Geometry.Expanded = true;
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Verify([mutation], [element]));
        element.Geometry = null;
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Verify([mutation], [element]));
    }

    [Theory]
    [InlineData("background")]
    [InlineData("border")]
    public void RequestedColorsMustSurviveReadbackExactly(string property)
    {
        var mutation = Create(); var element = Readback();
        if (property == "background") element.Geometry!.BackgroundArgb = 0;
        else element.Geometry!.BorderArgb = 0;
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Verify([mutation], [element]));
        if (property == "background") element.Geometry!.BackgroundArgb = null;
        else element.Geometry!.BorderArgb = null;
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Verify([mutation], [element]));
    }

    [Fact]
    public void UnrequestedColorsAreNotInventedAsReadbackPostconditions()
    {
        var mutation = Create(); mutation.Geometry!.BackgroundArgb = null; mutation.Geometry.BorderArgb = null;
        var element = Readback(); element.Geometry!.BackgroundArgb = 0; element.Geometry.BorderArgb = 1;
        NativeEditPlan.Verify([mutation], [element]);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("target")]
    [InlineData("point-count")]
    [InlineData("point-x")]
    [InlineData("point-y")]
    [InlineData("point-order")]
    public void ConnectorReadbackRejectsAnyEndpointOrPolylineDifference(string difference)
    {
        var mutation = Connector();
        NativeElement element = new()
        {
            Id = ElementId,
            SourceId = SourceId,
            TargetId = TargetId,
            Points = [new() { X = 0, Y = 10 }, new() { X = 30, Y = 40 }]
        };
        NativeEditPlan.Verify([mutation], [element]);
        if (difference == "source") element.SourceId = ParentId;
        if (difference == "target") element.TargetId = ParentId;
        if (difference == "point-count") element.Points = [];
        if (difference == "point-x") element.Points[1].X = 31;
        if (difference == "point-y") element.Points[0].Y = 11;
        if (difference == "point-order") Array.Reverse(element.Points);
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Verify([mutation], [element]));
    }

    [Fact]
    public void EveryMutationInTheBatchRequiresItsOwnReadbackPostcondition()
    {
        var first = Update(); var second = Update(); second.ElementId = SourceId; second.Name = "Second request";
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Verify([first, second], [Readback()]));
        NativeEditPlan.Verify([first, second], [Readback(), new() { Id = SourceId, Name = "Second request" }]);
    }
}
