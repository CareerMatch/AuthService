using AuthServices.Config;
using MongoDB.Driver;

namespace AuthServices.Repository;

using Models;


public class AuthRepository : IAuthRepository
{
    private readonly IMongoCollection<AuthModel> _authModels;

    public AuthRepository(MongoDbContext context)
    {
        _authModels = context.AuthModels;
    }

    public AuthModel GetUserByEmail(string email) =>
        _authModels.Find(user => user.Email == email).FirstOrDefault();

    public void AddUser(AuthModel user) =>
        _authModels.InsertOne(user);

    public void UpdateUser(AuthModel user) =>
        _authModels.ReplaceOne(u => u.Id == user.Id, user);
    
    public AuthModel GetUserByRefreshToken(string refreshToken) // Implement this method
    {
        return _authModels.Find(user => user.RefreshToken == refreshToken).FirstOrDefault();
    }
}