namespace Dealership.Infrastructure.Configuration;

public sealed class BillplzSettings
{
    public const string SectionName = "Billplz";

    // Off keeps the simulated deposit; on sends staff deposit links to the hosted payment page.
    public bool Enabled { get; set; }

    public string? ApiKey { get; set; }

    public string? XSignatureKey { get; set; }

    public string? CollectionId { get; set; }

    // The payment order collection refunds are paid from; refunds stay off while it is empty.
    public string? PaymentOrderCollectionId { get; set; }

    // The sandbox or production API root, for example https://www.billplz-sandbox.com/api/v3/.
    public string BaseUrl { get; set; } = "https://www.billplz-sandbox.com/api/v3/";

    // The address Billplz can reach this API on; a tunnel while the API runs locally.
    public string? PublicBaseUrl { get; set; }

    public bool IsComplete => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(XSignatureKey) &&
        !string.IsNullOrWhiteSpace(CollectionId) && Uri.TryCreate(BaseUrl, UriKind.Absolute, out var api) &&
        api.Scheme == Uri.UriSchemeHttps && Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out var site) &&
        (site.Scheme == Uri.UriSchemeHttps || site.IsLoopback);
}
