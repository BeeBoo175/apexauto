using Dealership.Application.Common;

namespace Dealership.Application.Accounts;

public static class CustomerAccountTabs
{
    public const string All = "All";
    public const string Unverified = "Unverified";
    public const string Suspended = "Suspended";

    public static bool IsValid(string? tab) => tab is null or All or Unverified or Suspended;
}

// What an Administrator may read of a customer's contact details: enough to recognise them in a list, never the whole.
public static class PersonalDataMasking
{
    // aisyah@example.com becomes a***@example.com.
    public static string Email(string email)
    {
        var at = email.LastIndexOf('@');
        return at <= 0 ? "***" : $"{email[0]}***{email[at..]}";
    }

    // 0123456789 becomes ******6789.
    public static string Mobile(string mobile)
    {
        var digits = mobile.Count(char.IsDigit);
        return digits <= 4 ? "****" : $"{new string('*', Math.Min(digits - 4, 8))}{new string([.. mobile.Where(char.IsDigit).TakeLast(4)])}";
    }

    // 940215101234 becomes ********1234; the full number is only ever shown through a logged reveal.
    public static string Nric(string nric) => nric.Length <= 4 ? "****" : $"{new string('*', nric.Length - 4)}{nric[^4..]}";
}

public sealed record CustomerDirectoryQuery(string Tab, string Search, int Page, int PageSize)
{
    public static CustomerDirectoryQuery? Parse(string? tab, string? search, int page, int pageSize)
    {
        var text = search?.Trim() ?? string.Empty;
        return !CustomerAccountTabs.IsValid(tab) || !ListSearch.IsValid(text) || !Pagination.IsValid(page, pageSize)
            ? null
            : new(tab ?? CustomerAccountTabs.All, text, page, pageSize);
    }
}

// The list shows masked contact only; the opened account shows the full details an Administrator needs to help.
public sealed record CustomerAccountRow(Guid Id, string Name, string Email, string Mobile, bool Verified, bool Suspended);

public sealed record CustomerAccountCounts(int All, int Unverified, int Suspended);

public sealed record CustomerAccountPage(CurrentActor Actor, IReadOnlyList<CustomerAccountRow> Items, int Total, int Page,
    int PageSize, CustomerAccountCounts Counts);

public sealed record CustomerAccountDetail(CurrentActor Actor, Guid Id, string Name, string Username, string Email,
    string Mobile, string Address, string Nric, bool Verified, bool Suspended);

public sealed record CustomerDirectoryResult<T>(StaffAccountReadStatus Status, T? Value = default);

public interface ICustomerAccountDirectory
{
    Task<CustomerDirectoryResult<CustomerAccountPage>> ListAsync(Guid administratorId, CustomerDirectoryQuery query,
        CancellationToken cancellationToken);

    Task<CustomerDirectoryResult<CustomerAccountDetail>> GetAsync(Guid administratorId, Guid customerId,
        CancellationToken cancellationToken);
}
