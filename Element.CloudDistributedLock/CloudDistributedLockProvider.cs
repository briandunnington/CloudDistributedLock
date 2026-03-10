namespace Element.CloudDistributedLock
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
            cosmosLockClient = new CosmosLockClient(options);
        }

        public async Task<CloudDistributedLock> AcquireLockAsync(string name, TimeSpan? timeout = null)
        {
            using var cancellationTokenSource = timeout.HasValue ? new CancellationTokenSource(timeout.Value) : new CancellationTokenSource();
            return await ContinuallyTryAcquireLockAsync(name, cancellationTokenSource.Token).ConfigureAwait(false);
        }

        public async Task<CloudDistributedLock> TryAcquireLockAsync(string name)
        {
            var item = await cosmosLockClient.TryAcquireLockAsync(name).ConfigureAwait(false);
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
            while (!cancellationToken.IsCancellationRequested)
            {
                var @lock = await TryAcquireLockAsync(name).ConfigureAwait(false);
                if (@lock.IsAcquired)
                {
                    return @lock;
                }

                @lock.Dispose();
                await Task.Delay(options.RetryInterval, cancellationToken).ConfigureAwait(false);
            }

            return CloudDistributedLock.CreateUnacquiredLock();
        }
    }
}