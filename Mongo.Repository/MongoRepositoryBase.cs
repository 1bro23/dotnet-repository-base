using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using Mongo.Repository.Domain.Entities;
using Mongo.Repository.Domain.Exceptions;
using Mongo.Repository.Resources;
using MongoDB.Driver;

namespace Mongo.Repository;

public abstract class MongoRepositoryBase<Model, Filter, Update> where Filter : FilterBase
{
    protected internal readonly IMongoCollection<Model> collection;
    protected internal readonly IMongoDatabase database;
    private readonly IHostApplicationLifetime lifetime;
    protected MongoRepositoryBase(
        IMongoClient client,
        string databaseName,
        string collectionName,
        IHostApplicationLifetime lifetime)
    {
        CheckIsIdFieldExists();
        database = client.GetDatabase(databaseName);
        collection = database.GetCollection<Model>(collectionName);
        this.lifetime = lifetime;
    }

    public Task<bool> ExistsAsync(Filter filter)
    {
        var fd = CreateCombinedFilter(filter);
        return collection.Find(fd).AnyAsync();
    }

    public async Task<List<Model>> FindAsync(Filter filter)
    {
        var ff = collection.Find(CreateCombinedFilter(filter));
        if (filter.sort != null)
            ff = ff.Sort(CreateSortDefinition(filter.sort));
        return await ff.ToListAsync(lifetime.ApplicationStopping);
    }

    public async Task<Model> FindByIdAsync<T>(T id, Expression<Func<Model, object>>? idTarget = null) where T : notnull
    {
        var filter = Builders<Model>.Filter;
        var fd = idTarget != null ? filter.Eq(idTarget, id) : filter.Eq("id", id);
        return await collection.Find(fd).FirstOrDefaultAsync();
    }

    public async Task InsertOneAsync(Model model)
    {
        try
        {
            await collection.InsertOneAsync(model, cancellationToken: lifetime.ApplicationStopping);
        }
        catch (MongoWriteException e)
        {
            if (e.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                var dupKey = Regex.Match(e.Message, @"key:\s*({.*?})").Groups[1].ToString().Replace("\"", "'");
                throw new HttpStatusException(409, string.Format(ResponseMessage.DataWithKey_AlreadyExists, dupKey));
            }
            throw;
        }
    }
    protected internal IAggregateFluent<Model> Aggregate(Filter filter)
    {
        var fd = CreateCombinedFilter(filter);
        var af = collection.Aggregate()
            .Match(fd);
        if (filter.sort != null)
            af = af.Sort(CreateSortDefinition(filter.sort));

        return af;
    }

    protected internal (IAggregateFluent<Model>, Task<AggregateCountResult>) AggregateWithPagination(Filter filter)
    {
        var af = Aggregate(filter);
        return (af.Skip((filter.pageIndex - 1) * filter.pageSize).Limit(filter.pageSize), af.Count().FirstAsync());
    }
    protected internal async Task<DeleteResult> DeleteManyAsync(Filter filter)
    {
        var fd = CreateCombinedFilter(filter);
        return await collection.DeleteManyAsync(fd, lifetime.ApplicationStopping);
    }

    protected internal async Task<Model> DeleteOneAsync(Filter filter)
    {
        var fd = CreateCombinedFilter(filter);
        return await collection.FindOneAndDeleteAsync(fd, cancellationToken: lifetime.ApplicationStopping);
    }
    protected internal async Task<List<TProjection>> FindAsync<TProjection>(
        Filter filter, Expression<Func<Model, TProjection>> project)
    {
        var ff = collection.Find(CreateCombinedFilter(filter));
        if (filter.sort != null)
            ff = ff.Sort(CreateSortDefinition(filter.sort));

        return await ff.Project(project).ToListAsync(lifetime.ApplicationStopping);
    }

    protected internal async Task<Model> FindOneAndUpdateAsync(Filter filter, Update update, bool getUpdatedValue = false)
    {
        var fd = CreateCombinedFilter(filter);
        var ud = CreateUpdateDefinition(update);
        if (getUpdatedValue)
            return await collection.FindOneAndUpdateAsync(fd, ud, new()
            {
                ReturnDocument = ReturnDocument.After
            }, lifetime.ApplicationStopping);
        else return await collection.FindOneAndUpdateAsync(fd, ud, cancellationToken: lifetime.ApplicationStopping);
    }

    protected internal async Task<PaginationResult<Model>> FindWithPaginationAsync(Filter filter)
    {
        IFindFluent<Model, Model> ff2;
        var ff = collection.Find(CreateCombinedFilter(filter));
        if (filter.sort != null)
            ff2 = ff.Sort(CreateSortDefinition(filter.sort));
        ff2 = ff.Skip((filter.pageIndex - 1) * filter.pageSize).Limit(filter.pageSize);

        var totalDocument = ff.CountDocumentsAsync(lifetime.ApplicationStopping);
        var list = await ff.ToListAsync(lifetime.ApplicationStopping);
        var mp = new MetaPagination(filter.pageIndex, filter.pageSize, await totalDocument);

        return new(list, mp);
    }

    protected internal async Task InsertManyAsync(List<Model> models)
    {
        await collection.InsertManyAsync(models, cancellationToken: lifetime.ApplicationStopping);
    }
    protected internal IAggregateFluent<TResult> Lookup<TBase, TForeign, TResult>(
        IAggregateFluent<TBase> af,
        string foreignCollection,
        Expression<Func<TBase, object>> localField,
        Expression<Func<TForeign, object>> foreignField,
        Expression<Func<TResult, object>> @as)
    {
        return af.Lookup(database.GetCollection<TForeign>(foreignCollection), localField, foreignField, @as)
            .Unwind<TResult, TBase>(@as)
            .Project<TResult>(Builders<TBase>.Projection.Exclude(localField));
    }

    protected internal async Task<Model> ReplaceAsync(Filter filter, Model model, bool isUpsert)
    {
        var fd = CreateCombinedFilter(filter);
        return await collection.FindOneAndReplaceAsync(fd, model, cancellationToken: lifetime.ApplicationStopping);
    }

    protected internal async IAsyncEnumerable<IEnumerable<Model>> StreamAsync(Filter filter)
    {
        var ff = collection.Find(CreateCombinedFilter(filter));
        if (filter.sort != null)
            ff = ff.Sort(CreateSortDefinition(filter.sort));

        using var cursor = await ff.ToCursorAsync();
        while (await cursor.MoveNextAsync()) yield return cursor.Current;
    }
    protected internal async IAsyncEnumerable<IEnumerable<T>> StreamAsync<T>(Filter filter, Expression<Func<Model, T>> project)
    {
        var ff = collection.Find(CreateCombinedFilter(filter));
        if (filter.sort != null)
            ff = ff.Sort(CreateSortDefinition(filter.sort));

        using var cursor = await ff.Project(project).ToCursorAsync();
        while (await cursor.MoveNextAsync()) yield return cursor.Current;
    }
    protected internal async Task<PaginationResult<TResult>> ToPaginationAsync<TResult>(
        IAggregateFluent<TResult> af, Filter filter, Task<AggregateCountResult> tdTask)
    {
        return new(await af.ToListAsync(), new(filter.pageIndex, filter.pageSize, (await tdTask).Count));
    }
    protected internal async Task<UpdateResult> UpdateManyAsync(Filter filter, Update update, bool isUpsert)
    {
        var fd = CreateCombinedFilter(filter);
        var ud = CreateUpdateDefinition(update);
        var ur = await collection.UpdateManyAsync(fd, ud, new() { IsUpsert = isUpsert }, lifetime.ApplicationStopping);
        return ur;
    }
    protected internal async Task<UpdateResult> UpdateOneAsync(Filter filter, Update update, bool isUpsert = false)
    {
        var fd = CreateCombinedFilter(filter);
        var ud = CreateUpdateDefinition(update);
        var ur = await collection.UpdateOneAsync(
            fd, ud, options: new() { IsUpsert = isUpsert }, cancellationToken: lifetime.ApplicationStopping);
        return ur;
    }

    #region helper
    protected internal abstract FilterDefinition<Model> CreateCombinedFilter(Filter filter);

    protected internal SortDefinition<Model> CreateSortDefinition(string sort)
    {
        var builder = Builders<Model>.Sort;
        var defaulValue = builder.Descending("_id");
        var part = sort.Split(['-', '.', '_']);
        if (part.Count() != 2)
            return defaulValue;
        var field = typeof(Model).GetProperty(part[0],
            BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
        if (field == null)
            return defaulValue;

        var dir = part[1].ToLower();
        if (dir == "asc")
            return builder.Ascending(field.Name);
        else if (dir == "desc")
            return builder.Descending(field.Name);
        else return defaulValue;
    }

    protected internal abstract UpdateDefinition<Model> CreateUpdateDefinition(Update update);

    protected internal FilterDefinition<Model> FromToFilter(string defaultField, Filter filter)
    {
        var builder = Builders<Model>.Filter;
        defaultField = filter.fromToKey ?? defaultField;
        if (defaultField == "id") defaultField = "_id";
        if (defaultField != "_id" && typeof(Model).GetProperty(defaultField) == null)
            throw new HttpStatusException(400, string.Format(ResponseMessage.Invalid_, "fromToKey"));

        var fd = builder.Empty;
        if (filter.from != null)
            fd &= builder.Gte(defaultField, filter.from.Value);
        if (filter.to != null)
            fd &= builder.Lt(defaultField, filter.to.Value);
        return fd;
    }

    private void CheckIsIdFieldExists()
    {
        if (typeof(Model).GetProperty("id") == null)
            throw new Exception("Model dosn't have field id.");
    }
    #endregion
}
