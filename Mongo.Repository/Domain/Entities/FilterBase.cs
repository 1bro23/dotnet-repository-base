public class FilterBase
{
    private int _pageSize = 10;
    private int _pageIndex = 1;
    public string? sort { get; set; }
    public string? fromToKey { get; set; }
    public long? from { get; set; }
    public long? to { get; set; }
    public int pageSize { get => _pageSize; set => _pageSize = Math.Max(1, value); }
    public int pageIndex { get => _pageIndex; set => _pageIndex = Math.Max(1, value); }
}