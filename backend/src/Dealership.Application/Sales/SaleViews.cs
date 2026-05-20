namespace Dealership.Application.Sales;

// The tab names the sale queue and manager table accept; the browser sends them verbatim.
public static class SaleViews
{
    public const string ToClaim = "ToClaim";
    public const string Handling = "Handling";
    public const string Waiting = "Waiting";
    public const string Settling = "Settling";
    public const string Completed = "Completed";
    public const string Approved = "Approved";
    public const string AwaitingHandover = "AwaitingHandover";
    public const string Delivered = "Delivered";
    public const string NeedsApproval = "NeedsApproval";
    public const string Open = "Open";
    public const string BeingHandled = "BeingHandled";
    public const string History = "History";
}
