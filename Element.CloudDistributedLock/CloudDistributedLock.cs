using Microsoft.Azure.Cosmos;

namespace Element.CloudDistributedLock
{
    public class CloudDistributedLock : IDisposable
    {
        private readonly TimeSpan keepAliveBuffer = TimeSpan.FromSeconds(1); // 1 second is the smallest Cosmos TTL increment
        private readonly CosmosLockClient? cosmosLockClient;
        private volatile ItemResponse<LockRecord>? latestItem;
        private readonly string? lockId;
        private readonly long fencingToken;
        private readonly CancellationTokenSource? cts;
        private readonly Task? keepAliveTask;
        private int disposed;


        public static CloudDistributedLock CreateUnacquiredLock()
        {
            return new CloudDistributedLock();
        }

        public static CloudDistributedLock CreateAcquiredLock(CosmosLockClient cosmosLockClient, ItemResponse<LockRecord> item)
        {
            return new CloudDistributedLock(cosmosLockClient, item);
        }

        private CloudDistributedLock()
        {
        }

        private CloudDistributedLock(CosmosLockClient cosmosLockClient, ItemResponse<LockRecord> item)
        {
            this.cosmosLockClient = cosmosLockClient;
            latestItem = item;
            fencingToken = SessionTokenParser.Parse(item.Headers.Session);
            lockId = $"{item.Resource.providerName}:{item.Resource.id}:{fencingToken}:{item.Resource.lockObtainedAt.Ticks}";
            cts = new CancellationTokenSource();
            keepAliveTask = KeepAliveLoop(item, cts.Token);
        }

        public bool IsAcquired => latestItem != null;

        public string? LockId => lockId;

        public long FencingToken => fencingToken;

        public string? ETag => latestItem?.ETag;

        private async Task KeepAliveLoop(ItemResponse<LockRecord> item, CancellationToken cancellationToken)
        {
            var current = item;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var lockRecord = current.Resource;
                    var lockExpiresAt = lockRecord!.lockLastRenewedAt + TimeSpan.FromSeconds(lockRecord._ttl);
                    var dueIn = lockExpiresAt - DateTimeOffset.UtcNow - keepAliveBuffer;

                    if (dueIn > TimeSpan.Zero)
                    {
                        await Task.Delay(dueIn, cancellationToken).ConfigureAwait(false);
                    }

                    var updatedItem = await cosmosLockClient!.RenewLockAsync(current).ConfigureAwait(false);
                    if (updatedItem == null) return;

                    current = updatedItem;
                    latestItem = updatedItem;
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch
                {
                    // swallow to prevent unobserved task exceptions; lock will expire via TTL
                    return;
                }
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (cts == null || Interlocked.Exchange(ref disposed, 1) != 0) return;
                cts.Cancel();
                keepAliveTask?.GetAwaiter().GetResult();
                cts.Dispose();
                ReleaseLock();
            }
        }

        private void ReleaseLock()
        {
            var item = latestItem;
            if (cosmosLockClient == null || item == null) return;

            // we want to do this synchronously to ensure the lock release/disposal is deterministic
            cosmosLockClient.ReleaseLockAsync(item).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        ~CloudDistributedLock()
        {
            Dispose(disposing: false);
        }
    }
}
