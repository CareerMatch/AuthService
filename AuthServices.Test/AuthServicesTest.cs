using Xunit;
using Moq;
using AuthServices.DTOs;
using AuthServices.Models;
using AuthServices.Repository;
using AuthServices.Services;
using Microsoft.Extensions.Configuration;
using System;

namespace AuthServices.Tests;

public class AuthServiceTests
{
    private readonly Mock<IAuthRepository> _authRepositoryMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _authRepositoryMock = new Mock<IAuthRepository>();
        _configurationMock = new Mock<IConfiguration>();

        // Mocking the configuration for JWT
        _configurationMock.Setup(config => config["JwtSettings:SecretKey"]).Returns("oIVH2c38/+tvPKZHFrIus4NfCua4wfeKUrkrZbMmV/Y=");
        _configurationMock.Setup(config => config["JwtSettings:Issuer"]).Returns("AuthService");
        _configurationMock.Setup(config => config["JwtSettings:Audience"]).Returns("AuthServiceAudience");

        // Initialize the AuthService with mocked dependencies
        _authService = new AuthService(_authRepositoryMock.Object, _configurationMock.Object);
    }

    [Fact]
    public void Register_User_Success()
    {
        // Arrange
        var registerDto = new RegisterDTO
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "johndoe@example.com",
            Password = "password123"
        };

        // Act
        var result = _authService.Register(registerDto);

        // Assert
        Assert.Equal("User registered successfully", result);
        _authRepositoryMock.Verify(repo => repo.AddUser(It.IsAny<AuthModel>()), Times.Once);
    }

    [Fact]
    public void Login_User_Success()
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

        _authRepositoryMock.Setup(repo => repo.GetUserByEmail(loginDto.Email)).Returns(user);

        // Act
        var result = _authService.Login(loginDto);

        // Assert
        Assert.NotNull(result.AccessToken);
        Assert.NotNull(result.RefreshToken);
        _authRepositoryMock.Verify(repo => repo.UpdateUser(It.IsAny<AuthModel>()), Times.Once);
    }

    [Fact]
    public void RefreshAccessToken_ValidToken_Success()
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

        _authRepositoryMock.Setup(repo => repo.GetUserByRefreshToken(refreshToken)).Returns(user);

        // Act
        var newAccessToken = _authService.RefreshAccessToken(refreshToken);

        // Assert
        Assert.NotNull(newAccessToken);
    }
}
