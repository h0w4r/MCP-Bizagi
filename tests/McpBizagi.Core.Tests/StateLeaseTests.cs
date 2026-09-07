using McpBizagi.Core;
using Xunit;

namespace McpBizagi.Core.Tests;

public sealed class StateLeaseTests
{
    [Fact] public void StateDirectoryCannotHaveTwoLiveWriters()
    {
        string root = Path.Combine(Path.GetTempPath(), "mcp-bizagi-lease-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var lease = new StateLease(root))
            {
                Assert.Throws<IOException>(() => new StateLease(root));
                // Operator diagnostics remain readable while the host owns the writer lease.
                using var reader = new StreamReader(new FileStream(Path.Combine(root, ".host.lock"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                Assert.Contains("pid", reader.ReadToEnd());
            }
            using var acquiredAfterRelease = new StateLease(root);
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
