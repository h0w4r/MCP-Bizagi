namespace McpBizagi.BizagiAdapter;

/// <summary>Only explicit MCP executable identities may resolve writable native preferences.</summary>
internal static class NativeSettingsIdentity
{
    internal static string RequireProduct(string? company, string? product)
    {
        if (company != "h0w4r" || (product != "McpBizagi.Worker" && product != "McpBizagi.LiveHost"))
            throw new InvalidOperationException("Native settings require a recognized MCP executable identity.");
        return product!;
    }
}
