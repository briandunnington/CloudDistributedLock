namespace CloudDistributedLock
{
    public interface ICloudDistributedLock
    {
        Task<CloudDistributedLock> TryAquireLockAsync(string name);

        Task<CloudDistributedLock> AcquireLockAsync(string name, TimeSpan? timeout = default);
    }

    public class CloudDistributedLockProvider : ICloudDistributedLock
    {
        private readonly CloudDistributedLockProviderOptions options;
        private readonly CosmosLockClient cosmosLockClient;

        public CloudDistributedLockProvider(CloudDistributedLockProviderOptions options)
        {
            this.options = options;
            this.cosmosLockClient = new CosmosLockClient(options);
        }

        public async Task<CloudDistributedLock> AcquireLockAsync(string name, TimeSpan? timeout = null)
        {
            using var cancellationTokenSource = timeout.HasValue ? new CancellationTokenSource(timeout.Value) : new CancellationTokenSource();
            return await ContinuallyTryAquireLockAsync(name, cancellationTokenSource.Token);
        }

        public async Task<CloudDistributedLock> TryAquireLockAsync(string name)
        {
            var item = await cosmosLockClient.TryAquireLockAsync(name);
            if (item != null)
            {
                return CloudDistributedLock.CreateAcquiredLock(cosmosLockClient, item);
            }
            else
            {
                return CloudDistributedLock.CreateUnacquiredLock();
            }
        }

        private async Task<CloudDistributedLock> ContinuallyTryAquireLockAsync(string name, CancellationToken cancellationToken)
        {
            CloudDistributedLock? @lock;
            do
            {
                @lock = await TryAquireLockAsync(name);
                if (!@lock.IsAcquired && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(options.RetryInterval);
                }
            }
            while (!@lock.IsAcquired && !cancellationToken.IsCancellationRequested);
            return @lock;
        }
    }
}