using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace CloudDistributedLock
{
    public interface ICloudDistributedLockProviderFactory
    {
        CloudDistributedLockProvider GetLockProvider();
        CloudDistributedLockProvider GetLockProvider(string name);
    }

    public class CloudDistributedLockProviderFactory : ICloudDistributedLockProviderFactory
    {
        internal const string DefaultName = "__DEFAULT";

        private readonly ConcurrentDictionary<string, CloudDistributedLockProvider> clients = new();


        public CloudDistributedLockProviderFactory(IOptionsMonitor<CloudDistributedLockProviderOptions> optionsMonitor)
        {
            this.OptionsMonitor = optionsMonitor;
        }

        protected IOptionsMonitor<CloudDistributedLockProviderOptions> OptionsMonitor { get; }

        public CloudDistributedLockProvider GetLockProvider(string name)
        {
            return clients.GetOrAdd(name, n => CreateClient(n));
        }

        public CloudDistributedLockProvider GetLockProvider()
        {
            return GetLockProvider(DefaultName);
        }

        protected CloudDistributedLockProvider CreateClient(string name)
        {
            var options = OptionsMonitor.Get(name);

            ArgumentNullException.ThrowIfNull(options.ProviderName);
            ArgumentNullException.ThrowIfNull(options.CosmosClient);
            ArgumentNullException.ThrowIfNull(options.DatabaseName);
            ArgumentNullException.ThrowIfNull(options.ContainerName);

            return new CloudDistributedLockProvider(options);
        }
    }
}
