namespace Dealership.Infrastructure.Storage;

public sealed class PhotoStorageSettings
{
    public string ServiceURL { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public bool ForcePathStyle { get; set; }
    public bool AllowInsecureHttp { get; set; }

    public void Validate()
    {
        if (!Uri.TryCreate(ServiceURL, UriKind.Absolute, out var endpoint) ||
            (endpoint.Scheme != Uri.UriSchemeHttps && !(endpoint.Scheme == Uri.UriSchemeHttp && (endpoint.IsLoopback || AllowInsecureHttp))) ||
            endpoint.UserInfo.Length > 0 || endpoint.AbsolutePath != "/" || endpoint.Query.Length > 0 || endpoint.Fragment.Length > 0 ||
            string.IsNullOrWhiteSpace(AccessKey) || string.IsNullOrWhiteSpace(SecretKey) || string.IsNullOrWhiteSpace(Region) ||
            BucketName.Length is < 3 or > 63 || BucketName.Any(character => character is not (>= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '-')))
        {
            throw new InvalidOperationException("Private photo storage requires explicit credentials, region, bucket and a secure endpoint.");
        }
    }
}
