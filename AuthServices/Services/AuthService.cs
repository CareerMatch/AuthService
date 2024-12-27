using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AuthServices.Config;
using AuthServices.DTOs;
using AuthServices.Models;
using AuthServices.Repository;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AuthServices.Services;

public class AuthService : IAuthService
{
    private readonly IAuthRepository _authRepository;
    private readonly JwtSettings _jwtSettings;
    
    private readonly IRabbitMQPublisher _rabbitMqPublisher;

    public AuthService(
        IAuthRepository authRepository,
        IOptions<JwtSettings> jwtSettings,
        IRabbitMQPublisher rabbitMqPublisher)
    {
        _authRepository = authRepository;
        _jwtSettings = jwtSettings.Value;
        _rabbitMqPublisher = rabbitMqPublisher;
    }

    public async Task<string> RegisterAsync(RegisterDTO dto)
    {
        // Step 1: Check if the user already exists in the database
        var existingUser = await _authRepository.GetUserByEmailAsync(dto.Email);
        if (existingUser != null)
            throw new Exception("User already exists");

        // Step 2: Create a new AuthModel object for the user
        var user = new AuthModel
        {
            UserId = Guid.NewGuid(),
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password), // Hash the password
            RefreshToken = GenerateRefreshToken(), // Generate a refresh token
            RefreshTokenExpiry = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays) // Set refresh token expiry
        };
        
        // Step 4: Serialize the RegisterDTO to JSON for RabbitMQ message
        var message = JsonSerializer.Serialize(new
        {
            UserId = user.UserId,
            dto.FirstName,
            dto.LastName,
            dto.DateOfBirth,
            Role = user.Role // Add the role explicitly
        });

        // Step 5: Send the message to RabbitMQ
        await _rabbitMqPublisher.SendRegisterMessageAsync(message);
        
        
        // Step 3: Save the new user to the database
        await _authRepository.AddUserAsync(user);

        // Step 6: Return a success message
        return "User registered successfully";
    }

    public async Task<RefreshTokenOutputDTO> LoginAsync(LoginDTO dto)
    {
        var user = await _authRepository.GetUserByEmailAsync(dto.Email);
        if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid credentials");

        var accessToken = GenerateAccessToken(user);
        var refreshToken = GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays);
        await _authRepository.UpdateUserAsync(user);

        return new RefreshTokenOutputDTO
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken
        };
    }

    public async Task<string> RefreshAccessTokenAsync(string refreshToken)
    {
        var user = await _authRepository.GetUserByRefreshTokenAsync(refreshToken);
        if (user == null || user.RefreshTokenExpiry <= DateTime.UtcNow)
            throw new UnauthorizedAccessException("Invalid or expired refresh token");

        return GenerateAccessToken(user);
    }

    public async Task LogoutAsync(string refreshToken)
    {
        var user = await _authRepository.GetUserByRefreshTokenAsync(refreshToken);
        if (user == null)
            throw new UnauthorizedAccessException("Invalid token");

        user.RefreshToken = null;
        await _authRepository.UpdateUserAsync(user);
    }

    private string GenerateAccessToken(AuthModel user)
    {
        var key = Encoding.UTF8.GetBytes(_jwtSettings.SecretKey);

        // Current UTC time
        var now = DateTime.UtcNow;

        // Define the token descriptor with a slight buffer
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            }),
            NotBefore = now, // Optional: Explicitly set NotBefore to current UTC time
            Expires = now.AddMinutes(_jwtSettings.AccessTokenExpiryMinutes).AddSeconds(1), // Add a buffer to Expires
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256),
            Issuer = _jwtSettings.Issuer,
            Audience = _jwtSettings.Audience
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    private string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }
    }
    
    public async Task DeleteUserAsync(Guid userId)
    {
        // Step 1: Notify the UserService via RabbitMQ
        var deleteMessage = JsonSerializer.Serialize(new { UserId = userId });
        await _rabbitMqPublisher.SendDeleteMessageAsync(deleteMessage);

        Console.WriteLine($"Delete event sent to RabbitMQ for UserId: {userId}");

        // Step 2: Delete the user in the Auth database
         _authRepository.DeleteUserAsync(userId);

        Console.WriteLine($"User with ID {userId} deleted from Auth database.");
    }
}