namespace AuthServices.Config;

using MongoDB.Driver;
using Models;
using Microsoft.Extensions.Options;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(IOptions<MongoDbSettings> settings)
    {
        var client = new MongoClient(settings.Value.ConnectionString);
        _database = client.GetDatabase(settings.Value.DatabaseName);
    }

    public IMongoCollection<AuthModel> AuthModels => _database.GetCollection<AuthModel>("AuthModels");
}