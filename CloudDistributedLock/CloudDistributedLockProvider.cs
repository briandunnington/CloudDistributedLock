namespace CloudDistributedLock
{
    public interface ICloudDistributedLockProvider
    {
        Task<CloudDistributedLock> TryAcquireLockAsync(string name);

        Task<CloudDistributedLock> AcquireLockAsync(string name, TimeSpan? timeout = default);
    }

    public class CloudDistributedLockProvider : ICloudDistributedLockProvider
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
            return await ContinuallyTryAcquireLockAsync(name, cancellationTokenSource.Token);
        }

        public async Task<CloudDistributedLock> TryAcquireLockAsync(string name)
        {
            var item = await cosmosLockClient.TryAcquireLockAsync(name);
            if (item != null)
            {
                return CloudDistributedLock.CreateAcquiredLock(cosmosLockClient, item);
            }
            else
            {
                return CloudDistributedLock.CreateUnacquiredLock();
            }
        }

        private async Task<CloudDistributedLock> ContinuallyTryAcquireLockAsync(string name, CancellationToken cancellationToken)
        {
            CloudDistributedLock? @lock;
            do
            {
                @lock = await TryAcquireLockAsync(name);
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