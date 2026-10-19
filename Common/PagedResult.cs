namespace MineSafety.Api.Common;

// A page of results. Lists can grow to thousands of reports, so we never send them all at once:
// the app asks for page 1 (20 items), then page 2, and so on.
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }           // how many there are in total, across all pages
}
