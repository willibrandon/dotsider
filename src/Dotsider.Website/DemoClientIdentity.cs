namespace Dotsider.Website;

internal static class DemoClientIdentity
{
    private const string UnknownClient = "unknown";

    internal static string GetPartitionKey(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;
        if (address is null)
            return UnknownClient;

        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        return address.ToString();
    }
}
