using Microsoft.AspNetCore.Mvc;
using AuthServices.DTOs;
using AuthServices.Services;

namespace AuthServices.Controller;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDTO dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        await _authService.RegisterAsync(dto);
        return Ok("User registered successfully");
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDTO dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var tokens = await _authService.LoginAsync(dto);
        return Ok(new { tokens.AccessToken, tokens.RefreshToken });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenDTO dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var newAccessToken = await _authService.RefreshAccessTokenAsync(dto.RefreshToken);
        return Ok(new { AccessToken = newAccessToken });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenDTO dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        await _authService.LogoutAsync(dto.RefreshToken);
        return Ok("Logged out successfully");
    }
    
    [HttpDelete("delete/{userId}")]
    public async Task<IActionResult> DeleteUser(Guid userId)
    {
        try
        {
            await _authService.DeleteUserAsync(userId);
            return Ok($"User with ID {userId} deleted successfully.");
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }
}
