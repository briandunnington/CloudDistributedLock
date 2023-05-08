# CloudDistributedLock
Provides a distributed locking mechanism based on Azure Cosmos DB to synchronize work across different cloud services.

## Concepts

The library works by leveraging Azure Cosmos DB's optimistic concurrency and time-to-live (TTL) features. When code tries to acquire a lock, an attempt is made to write a record to the database. If the record already exists, Cosmos will prevent the new record from being written, indicating that the lock is already held by another process. If the record doesnt exist, the record will be successfully written and the lock acquired.

While the lock is held, the database record will be periodically updated indicating that the lock is stil in use. If the holder process crashes, goes offline, or otherwise abandons the lock, the updates will stop and Cosmos's TTL feature will delete the record after a preset amount of time, ensuring that the lock does not get permanently stuck in a held state.

Each lock has a unique value (id) so that you can perform very granular locks (on a specific username vs the whole Users collection). Since the datbase is a shared resource, you can use these locks across processes, across machines, and even across different applications to synchronize work.

## Usage

You can either try to acquire a lock and return immediately if the lock cannot be acquired, or wait (indefinitely or for a timeout period) on the lock.

### Try to acquire the lock immediately

```csharp
var lockProvider = lockProviderFactory.GetLockProvider();
using var @lock = await lockProvider.TryAquireLockAsync(LockName);
if (@lock.IsAcquired)
{
    // do your critical work here
}
else
{
    // lock could not be acquired; already held by another instance
}
```

### Try to acquire the lock and wait forever until it is available

```csharp
var lockProvider = lockProviderFactory.GetLockProvider();
using var @lock = await lockProvider.AquireLockAsync(LockName);
if (@lock.IsAcquired)
{
    // do your critical work here
}
else
{
    // lock could not be acquired - should not happen
}
```

### Try to acquire the lock and wait a set amount of time before giving up

```csharp
var lockProvider = lockProviderFactory.GetLockProvider();
using var @lock = await lockProvider.AquireLockAsync(LockName, TimeSpan.FromSeconds(2));
if (@lock.IsAcquired)
{
    // do your critical work here
}
else
{
    // lock could not be acquired within 2 seconds
}
```

### Registering with dependency injection

You can either provide the Cosmos endpoint and key (and the library will construct the `CosmosClient` for you), or you can provide a pre-constructed `CosmosClient` object (useful if you are using RBAC permissions or other custom configuration).

```csharp
// provide endpoint and key
services.AddCloudDistributedLock(config["CosmosEndpoint"], config["CosmosKey"], config["DatabaseName"], 5);
```

```csharp
// provide CosmosClient
services.AddCloudDistributedLock(cosmosClient, config["DatabaseName"], 5);
```

You can also register multiple different named lock providers for different locking scenarios in the same app (ex: locking on product items to synchronize inventory, and also locking on user profiles to synchronize customer updates).

```csharp
services.AddCloudDistributedLock("InventoryLock", config["CosmosEndpoint"], config["CosmosKey"], config["DatabaseName"], 5);
services.AddCloudDistributedLock("CustomerLock", config["CosmosEndpoint"], config["CosmosKey"], config["DatabaseName"], 5);

// to fetch the named provider in your logic:
var lockProvider = lockProviderFactory.GetLockProvider("InventoryLock");
```

In all cases, registering with DI will make  `ICloudDistributedLockProviderFactory` available to be injected. You can take a dependency on it in your app code constructors with:

```csharp
public MyCode(ICloudDistributedLockProviderFactory lockProviderFactory) 
{
    //...
}
```

If the critical work you are locking around requires updating external systems, you can use the lock's fencing token which is a monotonically increasing value. (NOTE: the external system needs to know about the token and how to handle it/implement checking)

```csharp
var lockProvider = lockProviderFactory.GetLockProvider();
using var @lock = await lockProvider.TryAquireLockAsync(LockName);
if (@lock.IsAcquired)
{
    // use the fencing token
    await externalService.DoSomething(@lock.FencingToken);
}
else
{
    // lock could not be acquired; already held by another instance
}
```