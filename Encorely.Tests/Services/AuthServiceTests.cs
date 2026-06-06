using EncorelyApplication.DTOs;
using EncorelyApplication.Interfaces;
using EncorelyApplication.Services;
using EncorelyDomain.Events;
using EncorelyModels;
using EncorelyQuery.Interfaces;
using EncorelyRepository.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Encorely.Tests.Services;

public class AuthServiceTests
{
    private readonly IAuthService _sut;
    private readonly IUsuarioQueries _usuarioQueries = Substitute.For<IUsuarioQueries>();
    private readonly IUsuarioRepository _usuarioRepository = Substitute.For<IUsuarioRepository>();
    private readonly IKafkaProducer<UserSyncEvent> _kafkaProducer = Substitute.For<IKafkaProducer<UserSyncEvent>>();
    private readonly ILogger<AuthService> _logger = Substitute.For<ILogger<AuthService>>();

    public AuthServiceTests()
    {
        _usuarioRepository.CreateAsync(Arg.Any<Usuario>()).Returns(Guid.NewGuid());
        _sut = new AuthService(_usuarioQueries, _usuarioRepository, _kafkaProducer, _logger);
    }

    [Fact]
    public async Task AuthenticateWithSpotifyAsync_WhenNewUser_ShouldCreateUserAndProduceKafkaEvent()
    {
        // Arrange — AuthService hardcodea el spotifyId internamente (es un mock de MVP)
        _usuarioQueries.GetBySpotifyIdAsync(Arg.Any<string>()).Returns((Usuario?)null);
        var request = new SpotifyAuthRequest("spotify-access-token");

        // Act
        var userId = await _sut.AuthenticateWithSpotifyAsync(request);

        // Assert
        userId.Should().NotBe(Guid.Empty);
        await _usuarioRepository.Received(1).CreateAsync(Arg.Any<Usuario>());
        await _kafkaProducer.Received(1).ProduceAsync(
            KafkaTopics.UserDnaSync,
            Arg.Is<UserSyncEvent>(e => e.SpotifyToken == request.AccessToken),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuthenticateWithSpotifyAsync_WhenExistingUser_ShouldNotCreateUserAndStillProduceKafkaEvent()
    {
        // Arrange — AuthService usa "spotify_id_123" hardcodeado; simulamos que ese ID ya existe
        var existingUser = new Usuario { Id = Guid.NewGuid(), SpotifyId = "spotify_id_123" };
        _usuarioQueries.GetBySpotifyIdAsync("spotify_id_123").Returns(existingUser);
        var request = new SpotifyAuthRequest("spotify-access-token");

        // Act
        var userId = await _sut.AuthenticateWithSpotifyAsync(request);

        // Assert
        userId.Should().Be(existingUser.Id);
        await _usuarioRepository.DidNotReceive().CreateAsync(Arg.Any<Usuario>());
        await _kafkaProducer.Received(1).ProduceAsync(
            KafkaTopics.UserDnaSync,
            Arg.Is<UserSyncEvent>(e => e.SpotifyToken == request.AccessToken),
            Arg.Any<CancellationToken>());
    }
}
