using Xunit;
using Moq;
using AuthServices.DTOs;
using AuthServices.Models;
using AuthServices.Repository;
using AuthServices.Services;
using Microsoft.Extensions.Options;
using AuthServices.Config;
using Microsoft.Extensions.Configuration;

namespace AuthServices.Test.UnitTests
{
    public class AuthServiceTests
    {
        private readonly Mock<IAuthRepository> _authRepositoryMock;
        private readonly Mock<IOptions<JwtSettings>> _jwtSettingsMock;
        private readonly Mock<IRabbitMQPublisher> _rabbitMqPublisherMock;
        private readonly AuthService _authService;

        public AuthServiceTests()
        {
            _authRepositoryMock = new Mock<IAuthRepository>();

            // Mocking the JwtSettings configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
                .Build();

            var jwtSettings = configuration.GetSection("JwtSettings").Get<JwtSettings>();

            // Mock the JwtSettings
            _jwtSettingsMock = new Mock<IOptions<JwtSettings>>();
            _jwtSettingsMock.Setup(config => config.Value).Returns(jwtSettings);

            // Mock IRabbitMqPublisher
            _rabbitMqPublisherMock = new Mock<IRabbitMQPublisher>();
            _rabbitMqPublisherMock.Setup(publisher => publisher.SendRegisterMessageAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            _rabbitMqPublisherMock.Setup(publisher => publisher.SendDeleteMessageAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);


            // Initialize the AuthService with mocked dependencies
            _authService = new AuthService(
                _authRepositoryMock.Object,
                _jwtSettingsMock.Object,
                _rabbitMqPublisherMock.Object
            );
        }

        [Fact]
        public async Task Register_User_Success()
        {
            // Arrange
            var registerDto = new RegisterDTO
            {
                FirstName = "John",
                LastName = "Doe",
                Email = "johndoe@example.com",
                Password = "password123"
            };

            _authRepositoryMock.Setup(repo => repo.GetUserByEmailAsync(registerDto.Email))
                .ReturnsAsync((AuthModel)null);

            // Act
            var result = await _authService.RegisterAsync(registerDto);

            // Assert
            Assert.Equal("User registered successfully", result);
            _authRepositoryMock.Verify(repo => repo.AddUserAsync(It.IsAny<AuthModel>()), Times.Once);
            _rabbitMqPublisherMock.Verify(publisher => publisher.SendRegisterMessageAsync(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task Register_User_Fails_WhenEmailExists()
        {
            // Arrange
            var registerDto = new RegisterDTO
            {
                FirstName = "Jane",
                LastName = "Doe",
                Email = "janedoe@example.com",
                Password = "password123"
            };

            var existingUser = new AuthModel { Email = registerDto.Email };
            _authRepositoryMock.Setup(repo => repo.GetUserByEmailAsync(registerDto.Email))
                .ReturnsAsync(existingUser);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<Exception>(() => _authService.RegisterAsync(registerDto));
            Assert.Equal("User already exists", exception.Message);
            _authRepositoryMock.Verify(repo => repo.AddUserAsync(It.IsAny<AuthModel>()), Times.Never);
        }

        [Fact]
        public async Task Login_User_Success()
        {
            // Arrange
            var loginDto = new LoginDTO
            {
                Email = "johndoe@example.com",
                Password = "password123"
            };

            var user = new AuthModel
            {
                Id = Guid.NewGuid().ToString(),
                Email = "johndoe@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                Role = "User"
            };

            _authRepositoryMock.Setup(repo => repo.GetUserByEmailAsync(loginDto.Email))
                .ReturnsAsync(user);

            // Act
            var result = await _authService.LoginAsync(loginDto);

            // Assert
            Assert.NotNull(result.AccessToken);
            Assert.NotNull(result.RefreshToken);
            _authRepositoryMock.Verify(repo => repo.UpdateUserAsync(It.IsAny<AuthModel>()), Times.Once);
        }

        [Fact]
        public async Task RefreshAccessToken_ValidToken_Success()
        {
            // Arrange
            var refreshToken = "valid_refresh_token";
            var user = new AuthModel
            {
                Id = Guid.NewGuid().ToString(),
                Email = "johndoe@example.com",
                RefreshToken = refreshToken,
                RefreshTokenExpiry = DateTime.UtcNow.AddDays(1) // Ensure this is a future date
            };

            _authRepositoryMock.Setup(repo => repo.GetUserByRefreshTokenAsync(refreshToken))
                .ReturnsAsync(user); // Simulate finding the user with the refresh token.

            // Act
            var newAccessToken = await _authService.RefreshAccessTokenAsync(refreshToken);

            // Assert
            Assert.NotNull(newAccessToken);
            _authRepositoryMock.Verify(repo => repo.GetUserByRefreshTokenAsync(refreshToken), Times.Once);
        }

        [Fact]
        public async Task RefreshAccessToken_InvalidToken_ThrowsException()
        {
            // Arrange
            var invalidRefreshToken = "invalid_refresh_token";

            _authRepositoryMock.Setup(repo => repo.GetUserByRefreshTokenAsync(invalidRefreshToken))
                .ReturnsAsync((AuthModel)null);

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _authService.RefreshAccessTokenAsync(invalidRefreshToken));
        }

        [Fact]
        public async Task Logout_User_Success()
        {
            // Arrange
            var refreshToken = "valid_refresh_token";
            var user = new AuthModel
            {
                Id = Guid.NewGuid().ToString(),
                Email = "johndoe@example.com",
                RefreshToken = refreshToken,
                RefreshTokenExpiry = DateTime.UtcNow.AddDays(1)
            };

            _authRepositoryMock.Setup(repo => repo.GetUserByRefreshTokenAsync(refreshToken))
                .ReturnsAsync(user);

            // Act
            await _authService.LogoutAsync(refreshToken);

            // Assert
            _authRepositoryMock.Verify(repo => repo.UpdateUserAsync(It.Is<AuthModel>(u => u.RefreshToken == null)), Times.Once);
        }
    }
}
