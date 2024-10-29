using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AuthServices.DTOs;
using AuthServices.Models;
using AuthServices.Repository;
using Microsoft.IdentityModel.Tokens;

namespace AuthServices.Services;

public class AuthService : IAuthService
{
    private readonly IAuthRepository _authRepository;
    private readonly string _jwtSecretKey;
    private readonly string _issuer;
    private readonly string _audience;

    public AuthService(IAuthRepository authRepository, IConfiguration config)
    {
        _authRepository = authRepository;
        _jwtSecretKey = config["JwtSettings:SecretKey"];
        _issuer = config["JwtSettings:Issuer"];
        _audience = config["JwtSettings:Audience"];
    }

    public string Register(RegisterDTO dto)
    {
        if (_authRepository.GetUserByEmail(dto.Email) != null)
            throw new Exception("User already exists");

        var user = new AuthModel
        {
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            RefreshToken = GenerateRefreshToken(),
            RefreshTokenExpiry = DateTime.UtcNow.AddDays(7)
        };

        _authRepository.AddUser(user);
        return "User registered successfully";
    }

    public RefreshTokenOutputDTO Login(LoginDTO dto)
    {
        var user = _authRepository.GetUserByEmail(dto.Email);
        if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid credentials");

        // Generate tokens
        var accessToken = GenerateAccessToken(user);
        var refreshToken = GenerateRefreshToken();

        // Update user with new refresh token and expiry
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        _authRepository.UpdateUser(user);

        return new RefreshTokenOutputDTO
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken
        };
    }

    public string RefreshAccessToken(string refreshToken)
    {
        var user = _authRepository.GetUserByRefreshToken(refreshToken);
        if (user == null || user.RefreshTokenExpiry <= DateTime.UtcNow)
            throw new UnauthorizedAccessException("Invalid or expired refresh token");

        return GenerateAccessToken(user);
    }

    private string GenerateAccessToken(AuthModel user)
    {
        var key = Convert.FromBase64String(_jwtSecretKey);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            }),
            Expires = DateTime.UtcNow.AddMinutes(1),
            SigningCredentials =
                new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
            Issuer = _issuer,
            Audience = _audience
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
}