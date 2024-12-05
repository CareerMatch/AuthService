using AuthServices.DTOs;

namespace AuthServices.Services;

public interface IAuthService
{
    Task<string> RegisterAsync(RegisterDTO dto);
    
    Task<RefreshTokenOutputDTO> LoginAsync(LoginDTO dto);
    
    Task<string> RefreshAccessTokenAsync(string refreshToken);
    
    Task LogoutAsync(string refreshToken);
}