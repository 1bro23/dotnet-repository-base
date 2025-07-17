namespace Mongo.Repository.Domain.Entities;

public record PaginationResult<T>(IEnumerable<T> rowData, MetaPagination metaPagination);

