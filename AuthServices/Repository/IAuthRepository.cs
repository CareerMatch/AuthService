namespace AuthServices.Repository;
using Models;

public interface IAuthRepository
{
    AuthModel GetUserByEmail(string email);
    void AddUser(AuthModel user);
    void UpdateUser(AuthModel user);
    
    AuthModel GetUserByRefreshToken(string refreshToken);
}