namespace Dealership.Application.Common;

public static class Pagination
{
    public const int DefaultPageSize = 25;

    public static bool IsValid(int page, int pageSize)
    {
        return page >= 1 && pageSize is 10 or 25 or 50;
    }
}
