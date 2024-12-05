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
        EnsureIndexes();
    }

    private void EnsureIndexes()
    {
        _authModels.Indexes.CreateOne(new CreateIndexModel<AuthModel>(
            Builders<AuthModel>.IndexKeys.Ascending(user => user.Email),
            new CreateIndexOptions { Unique = true }));

        _authModels.Indexes.CreateOne(new CreateIndexModel<AuthModel>(
            Builders<AuthModel>.IndexKeys.Ascending(user => user.RefreshToken)));
    }

    public async Task<AuthModel> GetUserByEmailAsync(string email) =>
        await _authModels.Find(user => user.Email == email).FirstOrDefaultAsync();

    public async Task AddUserAsync(AuthModel user) =>
        await _authModels.InsertOneAsync(user);

    public async Task UpdateUserAsync(AuthModel user) =>
        await _authModels.ReplaceOneAsync(u => u.Id == user.Id, user);

    public async Task<AuthModel> GetUserByRefreshTokenAsync(string refreshToken) =>
        await _authModels.Find(user => user.RefreshToken == refreshToken).FirstOrDefaultAsync();
}