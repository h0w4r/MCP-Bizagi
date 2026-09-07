using McpBizagi.Contracts;
using McpBizagi.Core;
using Xunit;

namespace McpBizagi.Core.Tests;

public sealed class PublicationImagesTests
{
    // Dimensional matching is a publication guard, not proof of diagram pixels or native-engine behavior.
    private static NativeImageSize Size(int width, int height) => new() { Width = width, Height = height };

    [Fact]
    public void ExactMatchesReportSourceAndOutputDimensionsWithoutClaimingPixelEquivalence()
    {
        var matches = PublicationImages.Match([Size(1000, 600)], [Size(32, 32), Size(1000, 600)]);
        var match = Assert.Single(matches);
        Assert.Equal(new PublicationImageMatch(1000, 600, 1000, 600, false), match);
        Assert.Equal("dimensions_preserved_not_pixel_equivalence", match.Fidelity);
    }

    [Fact]
    public void MatchingUsesAMultisetRatherThanDeduplicatingEqualDimensions()
    {
        var expected = new[] { Size(800, 400), Size(800, 400) };
        var matches = PublicationImages.Match(expected, [Size(800, 400), Size(800, 400), Size(32, 32)]);
        Assert.Equal(2, matches.Length);
        Assert.All(matches, match => Assert.Equal(new PublicationImageMatch(800, 400, 800, 400, false), match));
        Assert.Throws<InvalidDataException>(() => PublicationImages.Match(expected, [Size(800, 400), Size(32, 32)]));
    }

    [Fact]
    public void DistinctImageOccurrencesAreCountedEvenWhenTheirDtoReferenceIsRepeated()
    {
        var actual = Size(800, 400);
        var matches = PublicationImages.Match([Size(800, 400), Size(800, 400)], [actual, actual]);
        Assert.Equal(2, matches.Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CoverLogosCannotSatisfyMissingDiagramImagesByCountAlone(bool consent)
    {
        Assert.Throws<InvalidDataException>(() => PublicationImages.Match(
            [Size(1000, 600), Size(1200, 800)], [Size(32, 32), Size(64, 64), Size(128, 128)], consent));
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, 0)]
    [InlineData(-1, 100)]
    [InlineData(100, -1)]
    [InlineData(int.MinValue, int.MinValue)]
    public void InvalidDimensionsAreRejectedForBothSourcesAndOutputs(int width, int height)
    {
        Assert.Throws<InvalidDataException>(() => PublicationImages.Match([Size(width, height)], [Size(100, 100)]));
        Assert.Throws<InvalidDataException>(() => PublicationImages.Match([Size(100, 100)], [Size(width, height)]));
        // An invalid unmatched image must not be silently discarded as though it were a harmless logo.
        Assert.Throws<InvalidDataException>(() => PublicationImages.Match([Size(100, 100)], [Size(100, 100), Size(width, height)]));
    }

    [Fact]
    public void DownsamplingRequiresExplicitConsentAndReportsReducedFidelity()
    {
        var expected = new[] { Size(1000, 600) }; var actual = new[] { Size(750, 450) };
        Assert.Throws<InvalidDataException>(() => PublicationImages.Match(expected, actual));
        Assert.Throws<InvalidDataException>(() => PublicationImages.Match(expected, actual, allowResampling: false));
        var match = Assert.Single(PublicationImages.Match(expected, actual, allowResampling: true));
        Assert.Equal(new PublicationImageMatch(1000, 600, 750, 450, true), match);
        Assert.Equal("explicitly_accepted_native_pdf_downsampling_not_pixel_equivalence", match.Fidelity);
    }

    [Fact]
    public void ResamplingConsentDoesNotReclassifyExactMatchesAsResampled()
    {
        Assert.False(Assert.Single(PublicationImages.Match([Size(1000, 600)], [Size(1000, 600)], true)).Resampled);
    }

    [Fact]
    public void ExactMatchesAreReservedBeforeAnyResamplingCandidateIsConsumed()
    {
        var matches = PublicationImages.Match([Size(1000, 1000), Size(700, 700)], [Size(700, 700), Size(500, 500)], true);
        Assert.Equal(2, matches.Length);
        Assert.Contains(new PublicationImageMatch(700, 700, 700, 700, false), matches);
        Assert.Contains(new PublicationImageMatch(1000, 1000, 500, 500, true), matches);
    }

    [Theory]
    [InlineData(499, 499)]
    [InlineData(400, 400)]
    [InlineData(1, 1)]
    public void MoreThanHalfTheLinearResolutionCannotBeDiscardedEvenWithConsent(int width, int height)
    {
        Assert.Throws<InvalidDataException>(() => PublicationImages.Match([Size(1000, 1000)], [Size(width, height)], true));
    }

    [Fact]
    public void HalfResolutionIsAcceptedAtTheInclusiveBoundary()
    {
        var match = Assert.Single(PublicationImages.Match([Size(1000, 600)], [Size(500, 300)], true));
        Assert.Equal(new PublicationImageMatch(1000, 600, 500, 300, true), match);
    }

    [Fact]
    public void HalfResolutionForOddDimensionsDoesNotRoundTheBoundDown()
    {
        Assert.Throws<InvalidDataException>(() => PublicationImages.Match([Size(1001, 1001)], [Size(500, 500)], true));
        Assert.True(Assert.Single(PublicationImages.Match([Size(1001, 1001)], [Size(501, 501)], true)).Resampled);
    }

    [Theory]
    [InlineData(1001, 600)]
    [InlineData(1000, 601)]
    [InlineData(1500, 900)]
    public void UpsamplingIsRejectedEvenWhenItsAspectRatioIsPreserved(int width, int height)
    {
        Assert.Throws<InvalidDataException>(() => PublicationImages.Match([Size(1000, 600)], [Size(width, height)], true));
    }

    [Theory]
    [InlineData(500, 503)]
    [InlineData(600, 700)]
    [InlineData(800, 500)]
    public void ResamplingMustPreserveAspectRatioWithinTheDeclaredTolerance(int width, int height)
    {
        Assert.Throws<InvalidDataException>(() => PublicationImages.Match([Size(1000, 1000)], [Size(width, height)], true));
    }

    [Fact]
    public void SmallRasterRoundingDifferencesWithinAspectToleranceAreAccepted()
    {
        var match = Assert.Single(PublicationImages.Match([Size(1000, 1000)], [Size(500, 501)], true));
        Assert.Equal(new PublicationImageMatch(1000, 1000, 500, 501, true), match);
    }

    [Fact]
    public void AspectToleranceIsInclusiveWithoutBinaryFloatingPointBoundaryRejection()
    {
        // The two linear scales are exactly 0.500 and 0.502 in rational pixel units.
        var match = Assert.Single(PublicationImages.Match([Size(1000, 1000)], [Size(500, 502)], true));
        Assert.Equal(new PublicationImageMatch(1000, 1000, 500, 502, true), match);
    }

    [Fact]
    public void AValidAssignmentIsFoundWhenTheLargestCandidateIsNeededByAnotherSource()
    {
        // A greedy 1000 -> 700 choice incorrectly strands the 1200 source, whose minimum width is 600.
        var matches = PublicationImages.Match([Size(1000, 1000), Size(1200, 1200)], [Size(700, 700), Size(500, 500)], true);
        Assert.Equal(2, matches.Length);
        Assert.Contains(new PublicationImageMatch(1000, 1000, 500, 500, true), matches);
        Assert.Contains(new PublicationImageMatch(1200, 1200, 700, 700, true), matches);
    }

    [Fact]
    public void AssignmentExistenceDoesNotDependOnSourceOrCandidateOrder()
    {
        foreach (bool reverseSources in new[] { false, true })
            foreach (bool reverseCandidates in new[] { false, true })
            {
                var sources = new[] { Size(1000, 1000), Size(1200, 1200) };
                var candidates = new[] { Size(700, 700), Size(500, 500) };
                if (reverseSources) Array.Reverse(sources);
                if (reverseCandidates) Array.Reverse(candidates);
                var matches = PublicationImages.Match(sources, candidates, true);
                Assert.Equal(2, matches.Length);
                Assert.Contains(new PublicationImageMatch(1000, 1000, 500, 500, true), matches);
                Assert.Contains(new PublicationImageMatch(1200, 1200, 700, 700, true), matches);
            }
    }

    [Fact]
    public void AResampledCandidateCannotBeReusedForMultipleSources()
    {
        Assert.Throws<InvalidDataException>(() => PublicationImages.Match(
            [Size(1000, 1000), Size(1200, 1200)], [Size(700, 700), Size(32, 32)], true));
    }

    [Fact]
    public void MultipleInterchangeableCandidatesStillProduceOneMatchPerSource()
    {
        var matches = PublicationImages.Match([Size(1000, 1000), Size(1100, 1100)], [Size(700, 700), Size(800, 800)], true);
        Assert.Equal(2, matches.Length);
        Assert.Equal(new[] { 1000, 1100 }, matches.Select(m => m.SourceWidth).Order().ToArray());
        Assert.Equal(new[] { 700, 800 }, matches.Select(m => m.OutputWidth).Order().ToArray());
        Assert.All(matches, m => Assert.True(m.Resampled));
    }

    [Fact]
    public void EmptyExpectedSetDoesNotInventDiagramMatches()
    {
        Assert.Empty(PublicationImages.Match([], []));
        Assert.Empty(PublicationImages.Match([], [Size(32, 32)]));
    }

    [Fact]
    public void InputsRemainUnmodifiedAfterCandidateReservation()
    {
        var source = Size(1000, 600); var actual = Size(750, 450);
        var expected = new[] { source }; var images = new[] { actual };
        PublicationImages.Match(expected, images, true);
        Assert.Same(source, Assert.Single(expected));
        Assert.Same(actual, Assert.Single(images));
        Assert.Equal((1000, 600), (source.Width, source.Height));
        Assert.Equal((750, 450), (actual.Width, actual.Height));
    }
}
