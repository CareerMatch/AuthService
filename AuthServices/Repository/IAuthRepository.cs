namespace AuthServices.Repository;
using Models;

public interface IAuthRepository
{
    Task<AuthModel> GetUserByEmailAsync(string email);
    
    Task AddUserAsync(AuthModel user);
    
    Task UpdateUserAsync(AuthModel user);
    
    Task<AuthModel> GetUserByRefreshTokenAsync(string refreshToken);
}