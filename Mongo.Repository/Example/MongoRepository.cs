using Microsoft.Extensions.Hosting;
using MongoDB.Driver;

namespace Mongo.Repository.Example;

public class MyModel { public int id { get; set; } public string name { get; set; } = null!; }
public class MyModelFilterDTO : FilterBase { public int? id { get; set; } }
public class MyModelUpdateDTO { public string? name { get; set; } }
public class MongoRepository : MongoRepositoryBase<MyModel, MyModelFilterDTO, MyModelUpdateDTO>
{
    public MongoRepository(
        IMongoClient client,
        IHostApplicationLifetime lifetime) : base(client, "yourDatabaseName", "yourCollectionName", lifetime)
    {
    }
    public Task<MyModel> DeleteByIdAsync(int id) => base.DeleteOneAsync(new() { id = id });

    protected internal override FilterDefinition<MyModel> CreateCombinedFilter(MyModelFilterDTO filter)
    {
        var b = Builders<MyModel>.Filter;
        var fd = b.Empty;

        if (filter.id != null) fd &= b.Eq(p => p.id, filter.id);

        return fd;
    }

    protected internal override UpdateDefinition<MyModel> CreateUpdateDefinition(MyModelUpdateDTO update)
    {
        var b = Builders<MyModel>.Update;
        var updates = new List<UpdateDefinition<MyModel>>();

        if (update.name != null) updates.Add(b.Set(p => p.name, update.name));

        return b.Combine(updates);
    }
}
