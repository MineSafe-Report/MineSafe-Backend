namespace MineSafety.Api.Common;

// One page of a longer list. The app asks for page 1, then page 2 and so on,
// so a list with thousands of reports is never sent in one go.
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; } // total number of items across all pages
}
