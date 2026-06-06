using EncorelyApplication.Exceptions;
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

public class IdentityServiceTests
{
    private readonly IIdentityService _sut;
    private readonly IUsuarioQueries _usuarioQueries = Substitute.For<IUsuarioQueries>();
    private readonly IUsuarioRepository _usuarioRepository = Substitute.For<IUsuarioRepository>();
    private readonly IMusicalProfileQueries _profileQueries = Substitute.For<IMusicalProfileQueries>();
    private readonly IMusicalProfileRepository _profileRepository = Substitute.For<IMusicalProfileRepository>();
    private readonly IKafkaProducer<UserSyncEvent> _kafkaProducer = Substitute.For<IKafkaProducer<UserSyncEvent>>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly ISpotifyService _spotifyService = Substitute.For<ISpotifyService>();
    private readonly ILogger<IdentityService> _logger = Substitute.For<ILogger<IdentityService>>();

    public IdentityServiceTests()
    {
        _tokenService.GenerateAccessToken(Arg.Any<Usuario>()).Returns("access-token");
        _tokenService.GenerateRefreshToken().Returns("refresh-token");
        _usuarioRepository.CreateAsync(Arg.Any<Usuario>()).Returns(Guid.NewGuid());
        _usuarioRepository.UpdateAsync(Arg.Any<Usuario>()).Returns(true);
        _profileRepository.CreateOrUpdateAsync(Arg.Any<MusicalProfile>()).Returns(Guid.NewGuid());

        _sut = new IdentityService(
            _usuarioQueries,
            _usuarioRepository,
            _profileQueries,
            _profileRepository,
            _kafkaProducer,
            _tokenService,
            _spotifyService,
            _logger);
    }

    [Fact]
    public async Task RegisterWithEmailAsync_WhenEmailNotTaken_ShouldCreateUserWithHashedPasswordAndReturnToken()
    {
        // Arrange
        const string email = "test@encorely.com";
        const string password = "SecurePass123";
        _usuarioQueries.GetByEmailAsync(email).Returns((Usuario?)null);

        // Act
        var result = await _sut.RegisterWithEmailAsync(email, password);

        // Assert
        result.AccessToken.Should().Be("access-token");
        result.RefreshToken.Should().Be("refresh-token");
        await _usuarioRepository.Received(1).CreateAsync(Arg.Is<Usuario>(u => u.Email == email && u.PasswordHash != null));
        var createdUser = _usuarioRepository.ReceivedCalls()
            .Single(c => c.GetMethodInfo().Name == nameof(IUsuarioRepository.CreateAsync))
            .GetArguments()[0] as Usuario;
        BCrypt.Net.BCrypt.Verify(password, createdUser!.PasswordHash!).Should().BeTrue();
    }

    [Fact]
    public async Task RegisterWithEmailAsync_WhenEmailAlreadyExists_ShouldThrowDuplicateEmailExceptionAndNotCreateUser()
    {
        // Arrange
        const string email = "existing@encorely.com";
        _usuarioQueries.GetByEmailAsync(email).Returns(new Usuario { Email = email });

        // Act & Assert
        await _sut.Invoking(s => s.RegisterWithEmailAsync(email, "pass123"))
            .Should().ThrowAsync<DuplicateEmailException>();

        await _usuarioRepository.DidNotReceive().CreateAsync(Arg.Any<Usuario>());
    }

    [Fact]
    public async Task LoginWithEmailAsync_WithValidCredentials_ShouldReturnTokenAndUpdateRefreshToken()
    {
        // Arrange
        const string email = "user@encorely.com";
        const string password = "Correct123";
        var hash = BCrypt.Net.BCrypt.HashPassword(password);
        var user = new Usuario { Id = Guid.NewGuid(), Email = email, PasswordHash = hash };
        _usuarioQueries.GetByEmailAsync(email).Returns(user);

        // Act
        var result = await _sut.LoginWithEmailAsync(email, password);

        // Assert
        result.AccessToken.Should().Be("access-token");
        result.UserId.Should().Be(user.Id);
        await _usuarioRepository.Received(1).UpdateAsync(Arg.Is<Usuario>(u => u.RefreshToken == "refresh-token"));
    }

    [Fact]
    public async Task LoginWithEmailAsync_WithWrongPassword_ShouldThrowInvalidCredentialsException()
    {
        // Arrange
        const string email = "user@encorely.com";
        var hash = BCrypt.Net.BCrypt.HashPassword("correct-password");
        _usuarioQueries.GetByEmailAsync(email).Returns(new Usuario { Email = email, PasswordHash = hash });

        // Act & Assert
        await _sut.Invoking(s => s.LoginWithEmailAsync(email, "wrong-password"))
            .Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Fact]
    public async Task LoginWithEmailAsync_WhenUserNotFound_ShouldThrowInvalidCredentialsException()
    {
        // Arrange
        _usuarioQueries.GetByEmailAsync(Arg.Any<string>()).Returns((Usuario?)null);

        // Act & Assert
        await _sut.Invoking(s => s.LoginWithEmailAsync("ghost@encorely.com", "anypass"))
            .Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Fact]
    public async Task LoginWithSpotifyAsync_WhenNewUser_ShouldCreateUserAndProduceKafkaEvent()
    {
        // Arrange
        const string spotifyToken = "spotify-access-token";
        _spotifyService.GetUserProfileAsync(spotifyToken, Arg.Any<CancellationToken>())
            .Returns(("spotify123", "user@example.com", "Display Name"));
        _usuarioQueries.GetBySpotifyIdAsync("spotify123").Returns((Usuario?)null);
        _usuarioQueries.GetByEmailAsync("user@example.com").Returns((Usuario?)null);
        _spotifyService.GenerateMusicalProfileAsync(spotifyToken, Arg.Any<CancellationToken>())
            .Returns(new MusicalProfile { Energy = 0.8, Danceability = 0.7, Valence = 0.6, Tempo = 120 });

        // Act
        var result = await _sut.LoginWithSpotifyAsync(spotifyToken);

        // Assert
        result.AccessToken.Should().Be("access-token");
        await _usuarioRepository.Received(1).CreateAsync(Arg.Is<Usuario>(u => u.SpotifyId == "spotify123"));
        await _kafkaProducer.Received(1).ProduceAsync(
            KafkaTopics.UserDnaSync,
            Arg.Is<UserSyncEvent>(e => e.SpotifyToken == spotifyToken),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginWithSpotifyAsync_WhenExistingUser_ShouldNotCreateUserAndStillProduceKafkaEvent()
    {
        // Arrange
        const string spotifyToken = "spotify-access-token";
        var existingUser = new Usuario { Id = Guid.NewGuid(), SpotifyId = "spotify123", Email = "user@example.com" };
        _spotifyService.GetUserProfileAsync(spotifyToken, Arg.Any<CancellationToken>())
            .Returns(("spotify123", "user@example.com", "Display Name"));
        _usuarioQueries.GetBySpotifyIdAsync("spotify123").Returns(existingUser);

        // Act
        var result = await _sut.LoginWithSpotifyAsync(spotifyToken);

        // Assert
        result.AccessToken.Should().Be("access-token");
        await _usuarioRepository.DidNotReceive().CreateAsync(Arg.Any<Usuario>());
        await _kafkaProducer.Received(1).ProduceAsync(
            KafkaTopics.UserDnaSync,
            Arg.Is<UserSyncEvent>(e => e.SpotifyToken == spotifyToken),
            Arg.Any<CancellationToken>());
    }
}
