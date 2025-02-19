using Microsoft.Azure.Cosmos;

namespace Element.CloudDistributedLock
{
    public class CloudDistributedLock : IDisposable
    {
        private readonly TimeSpan keepAliveBuffer = TimeSpan.FromSeconds(1); // 1 second is the smallest Cosmos TTL increment
        private readonly CosmosLockClient? cosmosLockClient;
        private ItemResponse<LockRecord>? currentItem;
        private readonly string? lockId;
        private readonly long fencingToken;
        private Timer? timer;
        private bool isDisposed;


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
            fencingToken = SessionTokenParser.Parse(item.Headers.Session);
            lockId = $"{item.Resource.providerName}:{item.Resource.id}:{fencingToken}:{item.Resource.lockObtainedAt.Ticks}";
            InitializeKeepAlive(item);
        }

        public bool IsAcquired => currentItem != null;

        public string? LockId => lockId;

        public long FencingToken => fencingToken;

        public string? ETag => currentItem?.ETag;

        async void KeepAlive(object? state)
        {
            if (!IsAcquired || isDisposed || cosmosLockClient == null || currentItem == null) return;

            var updatedItem = await cosmosLockClient.RenewLockAsync(currentItem);
            if (updatedItem != null)
            {
                InitializeKeepAlive(updatedItem);
            }
            else
            {
                // someone else already acquired a new lock, which means our lock was already released
            }
        }

        void InitializeKeepAlive(ItemResponse<LockRecord> item)
        {
            currentItem = item;

            if (!IsAcquired || isDisposed || item == null) return;

            var lockRecord = currentItem.Resource;
            var lockExpiresAt = lockRecord!.lockLastRenewedAt + TimeSpan.FromSeconds(lockRecord._ttl);
            var dueIn = lockExpiresAt - DateTimeOffset.UtcNow - keepAliveBuffer;  // renew the lock right before it expires if the reference is still held
            if (dueIn < TimeSpan.Zero) return;
            timer = new Timer(KeepAlive, null, dueIn, Timeout.InfiniteTimeSpan);
        }

        private void ReleaseLock()
        {
            if (cosmosLockClient == null || currentItem == null) return;

            // we want to do this synchronously to ensure the lock release/disposal is deterministic
            cosmosLockClient.ReleaseLockAsync(currentItem).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                isDisposed = true;

                // the lock in the DB is essentially an unmanaged resource
                timer?.Dispose();
                ReleaseLock();
            }
        }

        ~CloudDistributedLock()
        {
            Dispose(disposing: false);
        }
    }
}
