using EncorelyApplication.Services;
using EncorelyDomain.Entities;
using FluentAssertions;
using Xunit;

namespace Encorely.Tests.Services;

public class CompatibilityServiceTests
{
    private readonly CompatibilityService _sut;

    public CompatibilityServiceTests()
    {
        _sut = new CompatibilityService();
    }

    [Theory]
    [InlineData(1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0)] // Identical profiles
    [InlineData(0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 1.0)] // Half energy/danceability/valence but identical
    public void CalculateAffinity_IdenticalProfiles_ReturnsPerfectScore(
        double e1, double d1, double v1,
        double e2, double d2, double v2,
        double expected)
    {
        // Arrange
        var profileA = new MusicalProfile { Energy = e1, Danceability = d1, Valence = v1 };
        var profileB = new MusicalProfile { Energy = e2, Danceability = d2, Valence = v2 };

        // Act
        var result = _sut.CalculateAffinity(profileA, profileB);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void CalculateAffinity_OppositeProfiles_ReturnsLowScore()
    {
        // Arrange
        // Orthogonal vectors for Cosine Similarity should return 0
        var profileA = new MusicalProfile { Energy = 1.0, Danceability = 0.0, Valence = 0.0 };
        var profileB = new MusicalProfile { Energy = 0.0, Danceability = 1.0, Valence = 0.0 };

        // Act
        var result = _sut.CalculateAffinity(profileA, profileB);

        // Assert
        result.Should().Be(0.0);
    }

    [Theory]
    [InlineData(0.70, true)]
    [InlineData(0.69, false)]
    [InlineData(0.85, true)]
    public void IsCompatible_ShouldRespectiveThresholds(double affinity, bool expected)
    {
        // Act
        var result = _sut.IsCompatible(affinity);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(0.85, true)]
    [InlineData(0.84, false)]
    public void IsHighPriority_ShouldRespectiveThresholds(double affinity, bool expected)
    {
        // Act
        var result = _sut.IsHighPriority(affinity);

        // Assert
        result.Should().Be(expected);
    }
}
