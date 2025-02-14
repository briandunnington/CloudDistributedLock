using Microsoft.Azure.Cosmos;

namespace Element.CloudDistributedLock
{
    public class CloudDistributedLockProviderOptions
    {
        internal string? ProviderName { get; set; }

        internal CosmosClient? CosmosClient { get; set; }

        public int TTL { get; set; } = 5;   // Should match the value set in Cosmos

        public string? DatabaseName { get; set; }

        public string ContainerName { get; set; } = "distributedlocks";

        public TimeSpan RetryInterval { get; set; } = TimeSpan.FromMilliseconds(500);
    }
}
