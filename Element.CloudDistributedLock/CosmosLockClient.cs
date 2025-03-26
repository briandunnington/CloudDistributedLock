using Microsoft.Azure.Cosmos;
using System.Net;

namespace Element.CloudDistributedLock
{
    public class CosmosLockClient
    {
        private readonly CloudDistributedLockProviderOptions options;
        private readonly Container container;

        public CosmosLockClient(CloudDistributedLockProviderOptions options)
        {
            this.options = options;
            container = options.CosmosClient!.GetContainer(options.DatabaseName, options.ContainerName);
        }

        public async Task<ItemResponse<LockRecord>?> TryAcquireLockAsync(string name)
        {
            try
            {
                /* This will successfully insert the document if no other process is currently holding a lock.
                 * The collection is set with a TTL so that the record will be deleted automatically, 
                 * releasing the lock in the event that it is not released by the holder.
                 * */
                var safeLockName = GenerateSafeLockName(name);
                var now = DateTimeOffset.UtcNow;
                var lockRecord = new LockRecord { id = safeLockName, name = name, providerName = options.ProviderName, lockObtainedAt = now, lockLastRenewedAt = now, _ttl = options.TTL };
                return await container.CreateItemAsync(lockRecord, new PartitionKey(lockRecord.id));
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                // lock already held by someone else
                return null;
            }
        }

        public async Task<ItemResponse<LockRecord>?> RenewLockAsync(ItemResponse<LockRecord> item)
        {
            try
            {
                var lockRecord = item.Resource;
                lockRecord.lockLastRenewedAt = DateTimeOffset.UtcNow;
                return await container.UpsertItemAsync(lockRecord, new PartitionKey(lockRecord.id), new ItemRequestOptions { IfMatchEtag = item.ETag });
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                // someone else already acquired a new lock, which means our lock was already released
                return null;
        }
        }

        public async Task ReleaseLockAsync(ItemResponse<LockRecord> item)
        {
            try
            {
                var lockRecord = item.Resource;
                _ = await container.DeleteItemAsync<LockRecord>(lockRecord.id, new PartitionKey(lockRecord.id), new ItemRequestOptions { IfMatchEtag = item.ETag });
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                // someone else already acquired a new lock, which means our lock was already released
            }
        }


        private static string GenerateSafeLockName(string lockName)
        {
            //'/', '\\', '?', '#' are invalid 
            return lockName.Replace('/', '_').Replace('\\', '_').Replace('?', '_').Replace('#', '_');
        }
    }
}
