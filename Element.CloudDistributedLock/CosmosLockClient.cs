using Microsoft.Azure.Cosmos;
using System.Net;

namespace Element.CloudDistributedLock
{
    internal interface ICosmosLockClient
    {
        Task<ItemResponse<LockRecord>?> TryAcquireLockAsync(string name);
        Task<ItemResponse<LockRecord>?> RenewLockAsync(ItemResponse<LockRecord> item);
        Task ReleaseLockAsync(ItemResponse<LockRecord> item);
    }

    internal class CosmosLockClient : ICosmosLockClient
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
                return await container.CreateItemAsync(lockRecord, new PartitionKey(lockRecord.id)).ConfigureAwait(false);
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
                var existing = item.Resource;
                var lockRecord = new LockRecord
                {
                    id = existing.id,
                    name = existing.name,
                    providerName = existing.providerName,
                    lockObtainedAt = existing.lockObtainedAt,
                    lockLastRenewedAt = DateTimeOffset.UtcNow,
                    _ttl = existing._ttl
                };
                return await container.ReplaceItemAsync(lockRecord, lockRecord.id, new PartitionKey(lockRecord.id), new ItemRequestOptions { IfMatchEtag = item.ETag }).ConfigureAwait(false);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                // someone else already acquired a new lock, which means our lock was already released
                return null;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // lock record already expired via TTL
                return null;
            }
        }

        public async Task ReleaseLockAsync(ItemResponse<LockRecord> item)
        {
            try
            {
                var lockRecord = item.Resource;
                _ = await container.DeleteItemAsync<LockRecord>(lockRecord.id, new PartitionKey(lockRecord.id), new ItemRequestOptions { IfMatchEtag = item.ETag }).ConfigureAwait(false);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                // someone else already acquired a new lock, which means our lock was already released
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // lock record already expired via TTL
            }
        }


        private static string GenerateSafeLockName(string lockName)
        {
            //'/', '\\', '?', '#' are invalid 
            return lockName.Replace('/', '_').Replace('\\', '_').Replace('?', '_').Replace('#', '_');
        }
    }
}
