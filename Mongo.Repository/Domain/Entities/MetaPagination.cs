namespace Mongo.Repository.Domain.Entities;

public class MetaPagination
{
    public int pageIndex { get; set; }
    public int pageSize { get; set; }
    public long pageCount { get; set; }
    public long dataCount { get; set; }
    public string info { get; set; }
    public MetaPagination(int index, int size, long totalDocument)
    {
        pageIndex = Math.Max(1, index);
        pageSize = Math.Max(1, size);
        pageCount = (totalDocument + size - 1) / pageSize;
        dataCount = Math.Max(Math.Min(totalDocument - (pageIndex - 1) * pageSize, size), 0);

        var currentIndex = (pageIndex - 1) * pageSize;
        if (currentIndex > totalDocument) currentIndex = 0;

        info = $"Data {(totalDocument == 0 ? 0 : currentIndex + 1)} ~ {currentIndex + dataCount} of {totalDocument}";
    }
}
