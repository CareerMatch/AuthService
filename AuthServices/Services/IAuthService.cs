using AuthServices.DTOs;

namespace AuthServices.Services;

public interface IAuthService
{
    string Register(RegisterDTO registerDto);
    RefreshTokenOutputDTO Login(LoginDTO loginDto);
    
    string RefreshAccessToken(string refreshToken);
}