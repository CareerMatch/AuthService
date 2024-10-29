using Microsoft.AspNetCore.Mvc;
using AuthServices.DTOs;
using AuthServices.Services;

namespace AuthServices.Controller;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public IActionResult Register([FromBody] RegisterDTO dto)
    {
        _authService.Register(dto);
        return Ok("User registered successfully");
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginDTO dto)
    {
        var tokens = _authService.Login(dto);
        return Ok(new { tokens.AccessToken, tokens.RefreshToken });
    }

    [HttpPost("refresh")]
    public IActionResult Refresh([FromBody] RefreshTokenDTO dto)
    {
        var newAccessToken = _authService.RefreshAccessToken(dto.RefreshToken);
        return Ok(new { AccessToken = newAccessToken });
    }
}
