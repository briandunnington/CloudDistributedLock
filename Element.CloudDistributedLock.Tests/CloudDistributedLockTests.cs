using Microsoft.Azure.Cosmos;
using NSubstitute;

namespace Element.CloudDistributedLock.Tests
{
    public class CloudDistributedLockTests
    {
        private static ItemResponse<LockRecord> CreateMockItemResponse(LockRecord lockRecord, string etag = "etag-1", string sessionToken = "0:1#100#1=100")
        {
            var headers = new Headers { { "x-ms-session-token", sessionToken } };

            var response = Substitute.For<ItemResponse<LockRecord>>();
            response.Resource.Returns(lockRecord);
            response.ETag.Returns(etag);
            response.Headers.Returns(headers);
            return response;
        }

        private static LockRecord CreateLockRecord(int ttl = 5)
        {
            var now = DateTimeOffset.UtcNow;
            return new LockRecord
            {
                id = "test-lock",
                name = "test-lock",
                providerName = "test-provider",
                lockObtainedAt = now,
                lockLastRenewedAt = now,
                _ttl = ttl
            };
        }

        #region Unacquired Lock Tests

        [Fact]
        public void CreateUnacquiredLock_IsAcquired_ReturnsFalse()
        {
            var @lock = CloudDistributedLock.CreateUnacquiredLock();
            Assert.False(@lock.IsAcquired);
        }

        [Fact]
        public void CreateUnacquiredLock_LockId_ReturnsNull()
        {
            var @lock = CloudDistributedLock.CreateUnacquiredLock();
            Assert.Null(@lock.LockId);
        }

        [Fact]
        public void CreateUnacquiredLock_ETag_ReturnsNull()
        {
            var @lock = CloudDistributedLock.CreateUnacquiredLock();
            Assert.Null(@lock.ETag);
        }

        [Fact]
        public void CreateUnacquiredLock_FencingToken_ReturnsZero()
        {
            var @lock = CloudDistributedLock.CreateUnacquiredLock();
            Assert.Equal(0, @lock.FencingToken);
        }

        [Fact]
        public void CreateUnacquiredLock_Dispose_DoesNotThrow()
        {
            var @lock = CloudDistributedLock.CreateUnacquiredLock();
            @lock.Dispose();
        }

        [Fact]
        public void CreateUnacquiredLock_DoubleDispose_DoesNotThrow()
        {
            var @lock = CloudDistributedLock.CreateUnacquiredLock();
            @lock.Dispose();
            @lock.Dispose();
        }

        #endregion

        #region Acquired Lock Tests

        [Fact]
        public void CreateAcquiredLock_IsAcquired_ReturnsTrue()
        {
            var cosmosLockClient = Substitute.For<ICosmosLockClient>();
            cosmosLockClient.RenewLockAsync(Arg.Any<ItemResponse<LockRecord>>()).Returns((ItemResponse<LockRecord>?)null);
            cosmosLockClient.ReleaseLockAsync(Arg.Any<ItemResponse<LockRecord>>()).Returns(Task.CompletedTask);
            var item = CreateMockItemResponse(CreateLockRecord());

            using var @lock = CloudDistributedLock.CreateAcquiredLock(cosmosLockClient, item);
            Assert.True(@lock.IsAcquired);
        }

        [Fact]
        public void CreateAcquiredLock_LockId_IsNotNull()
        {
            var cosmosLockClient = Substitute.For<ICosmosLockClient>();
            cosmosLockClient.RenewLockAsync(Arg.Any<ItemResponse<LockRecord>>()).Returns((ItemResponse<LockRecord>?)null);
            cosmosLockClient.ReleaseLockAsync(Arg.Any<ItemResponse<LockRecord>>()).Returns(Task.CompletedTask);
            var item = CreateMockItemResponse(CreateLockRecord());

            using var @lock = CloudDistributedLock.CreateAcquiredLock(cosmosLockClient, item);
            Assert.NotNull(@lock.LockId);
            Assert.Contains("test-provider", @lock.LockId);
            Assert.Contains("test-lock", @lock.LockId);
        }

        [Fact]
        public void CreateAcquiredLock_ETag_ReturnsItemETag()
        {
            var cosmosLockClient = Substitute.For<ICosmosLockClient>();
            cosmosLockClient.RenewLockAsync(Arg.Any<ItemResponse<LockRecord>>()).Returns((ItemResponse<LockRecord>?)null);
            cosmosLockClient.ReleaseLockAsync(Arg.Any<ItemResponse<LockRecord>>()).Returns(Task.CompletedTask);
            var item = CreateMockItemResponse(CreateLockRecord(), etag: "my-etag");

            using var @lock = CloudDistributedLock.CreateAcquiredLock(cosmosLockClient, item);
            Assert.Equal("my-etag", @lock.ETag);
        }

        [Fact]
        public void CreateAcquiredLock_FencingToken_ParsedFromSessionToken()
        {
            var cosmosLockClient = Substitute.For<ICosmosLockClient>();
            cosmosLockClient.RenewLockAsync(Arg.Any<ItemResponse<LockRecord>>()).Returns((ItemResponse<LockRecord>?)null);
            cosmosLockClient.ReleaseLockAsync(Arg.Any<ItemResponse<LockRecord>>()).Returns(Task.CompletedTask);
            var item = CreateMockItemResponse(CreateLockRecord(), sessionToken: "0:1#200#1=200");

            using var @lock = CloudDistributedLock.CreateAcquiredLock(cosmosLockClient, item);
            Assert.Equal(200, @lock.FencingToken);
        }

        #endregion

        #region Dispose Tests

        [Fact]
        public void Dispose_ReleasesLock()
        {
            var cosmosLockClient = Substitute.For<ICosmosLockClient>();
            cosmosLockClient.RenewLockAsync(Arg.Any<ItemResponse<LockRecord>>()).Returns((ItemResponse<LockRecord>?)null);
            cosmosLockClient.ReleaseLockAsync(Arg.Any<ItemResponse<LockRecord>>()).Returns(Task.CompletedTask);
            var item = CreateMockItemResponse(CreateLockRecord());

            var @lock = CloudDistributedLock.CreateAcquiredLock(cosmosLockClient, item);
            @lock.Dispose();

            cosmosLockClient.Received(1).ReleaseLockAsync(Arg.Any<ItemResponse<LockRecord>>());
        }

        [Fact]
        public void Dispose_CalledTwice_ReleasesLockOnlyOnce()
        {
            var cosmosLockClient = Substitute.For<ICosmosLockClient>();
            cosmosLockClient.RenewLockAsync(Arg.Any<ItemResponse<LockRecord>>()).Returns((ItemResponse<LockRecord>?)null);
            cosmosLockClient.ReleaseLockAsync(Arg.Any<ItemResponse<LockRecord>>()).Returns(Task.CompletedTask);
            var item = CreateMockItemResponse(CreateLockRecord());

            var @lock = CloudDistributedLock.CreateAcquiredLock(cosmosLockClient, item);
            @lock.Dispose();
            @lock.Dispose();

            cosmosLockClient.Received(1).ReleaseLockAsync(Arg.Any<ItemResponse<LockRecord>>());
        }

        #endregion

        #region KeepAlive Tests

        [Fact]
        public async Task KeepAlive_RenewsLockBeforeTTLExpires()
        {
            var renewCalled = new TaskCompletionSource<bool>();

            var cosmosLockClient = Substitute.For<ICosmosLockClient>();
            cosmosLockClient.RenewLockAsync(Arg.Any<ItemResponse<LockRecord>>()).Returns(callInfo =>
            {
                renewCalled.TrySetResult(true);
                return (ItemResponse<LockRecord>?)null; // stop the loop after first renewal attempt
            });
            cosmosLockClient.ReleaseLockAsync(Arg.Any<ItemResponse<LockRecord>>()).Returns(Task.CompletedTask);

            var lockRecord = CreateLockRecord(ttl: 2);
            var item = CreateMockItemResponse(lockRecord);

            using var @lock = CloudDistributedLock.CreateAcquiredLock(cosmosLockClient, item);

            var renewed = await Task.WhenAny(renewCalled.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.Same(renewCalled.Task, renewed);
        }

        [Fact]
        public async Task KeepAlive_WhenRenewalFails_LoopExitsGracefully()
        {
            var renewCallCount = 0;
            var cosmosLockClient = Substitute.For<ICosmosLockClient>();
            cosmosLockClient.RenewLockAsync(Arg.Any<ItemResponse<LockRecord>>()).Returns(callInfo =>
            {
                Interlocked.Increment(ref renewCallCount);
                return (ItemResponse<LockRecord>?)null;
            });
            cosmosLockClient.ReleaseLockAsync(Arg.Any<ItemResponse<LockRecord>>()).Returns(Task.CompletedTask);

            var lockRecord = CreateLockRecord(ttl: 2);
            var item = CreateMockItemResponse(lockRecord);

            using var @lock = CloudDistributedLock.CreateAcquiredLock(cosmosLockClient, item);

            // wait for the renewal to be attempted and the loop to exit
            await Task.Delay(TimeSpan.FromSeconds(3));

            // should have been called exactly once - renewal returned null so the loop stopped
            Assert.Equal(1, renewCallCount);
        }

        [Fact]
        public async Task KeepAlive_WhenRenewalThrows_LoopExitsGracefully()
        {
            var cosmosLockClient = Substitute.For<ICosmosLockClient>();
            cosmosLockClient.RenewLockAsync(Arg.Any<ItemResponse<LockRecord>>()).Returns<ItemResponse<LockRecord>?>(_ =>
            {
                throw new InvalidOperationException("simulated failure");
            });
            cosmosLockClient.ReleaseLockAsync(Arg.Any<ItemResponse<LockRecord>>()).Returns(Task.CompletedTask);

            var lockRecord = CreateLockRecord(ttl: 2);
            var item = CreateMockItemResponse(lockRecord);

            // should not throw - the exception is swallowed inside the keep-alive loop
            using var @lock = CloudDistributedLock.CreateAcquiredLock(cosmosLockClient, item);
            await Task.Delay(TimeSpan.FromSeconds(3));

            @lock.Dispose(); // should not throw
        }

        [Fact]
        public async Task Dispose_DuringInFlightRenewal_WaitsForRenewalThenReleases()
        {
            var renewalStarted = new TaskCompletionSource<bool>();
            var allowRenewalToComplete = new TaskCompletionSource<bool>();

            var renewedItem = CreateMockItemResponse(CreateLockRecord(), etag: "etag-2", sessionToken: "0:1#200#1=200");

            var cosmosLockClient = Substitute.For<ICosmosLockClient>();
            cosmosLockClient.RenewLockAsync(Arg.Any<ItemResponse<LockRecord>>()).Returns(async callInfo =>
            {
                renewalStarted.SetResult(true);
                await allowRenewalToComplete.Task;
                return renewedItem;
            });
            cosmosLockClient.ReleaseLockAsync(Arg.Any<ItemResponse<LockRecord>>()).Returns(Task.CompletedTask);

            var lockRecord = CreateLockRecord(ttl: 2);
            var item = CreateMockItemResponse(lockRecord);

            var @lock = CloudDistributedLock.CreateAcquiredLock(cosmosLockClient, item);

            // wait for the renewal to start
            await renewalStarted.Task;

            // start dispose on another thread (it will block waiting for keepAliveTask)
            var disposeTask = Task.Run(() => @lock.Dispose());

            // give dispose a moment to call Cancel
            await Task.Delay(100);

            // allow the in-flight renewal to complete
            allowRenewalToComplete.SetResult(true);

            // dispose should complete without hanging
            var completedTask = await Task.WhenAny(disposeTask, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.Same(disposeTask, completedTask);

            // release should have been called with the renewed item (etag-2)
            cosmosLockClient.Received(1).ReleaseLockAsync(renewedItem);
        }

        [Fact]
        public async Task KeepAlive_SuccessfulRenewal_UpdatesETag()
        {
            var renewCount = 0;
            var renewedItem = CreateMockItemResponse(CreateLockRecord(), etag: "etag-2", sessionToken: "0:1#200#1=200");

            var cosmosLockClient = Substitute.For<ICosmosLockClient>();
            cosmosLockClient.RenewLockAsync(Arg.Any<ItemResponse<LockRecord>>()).Returns(callInfo =>
            {
                if (Interlocked.Increment(ref renewCount) == 1)
                    return renewedItem;
                return (ItemResponse<LockRecord>?)null; // stop after first successful renewal
            });
            cosmosLockClient.ReleaseLockAsync(Arg.Any<ItemResponse<LockRecord>>()).Returns(Task.CompletedTask);

            var lockRecord = CreateLockRecord(ttl: 2);
            var item = CreateMockItemResponse(lockRecord);

            using var @lock = CloudDistributedLock.CreateAcquiredLock(cosmosLockClient, item);

            // wait for at least one renewal to complete
            await Task.Delay(TimeSpan.FromSeconds(3));

            Assert.Equal("etag-2", @lock.ETag);
        }

        [Fact]
        public void CreateAcquiredLock_LockId_ContainsExpectedComponents()
        {
            var cosmosLockClient = Substitute.For<ICosmosLockClient>();
            cosmosLockClient.RenewLockAsync(Arg.Any<ItemResponse<LockRecord>>()).Returns((ItemResponse<LockRecord>?)null);
            cosmosLockClient.ReleaseLockAsync(Arg.Any<ItemResponse<LockRecord>>()).Returns(Task.CompletedTask);
            var item = CreateMockItemResponse(CreateLockRecord(), sessionToken: "0:1#100#1=100");

            using var @lock = CloudDistributedLock.CreateAcquiredLock(cosmosLockClient, item);

            // LockId format: {providerName}:{id}:{fencingToken}:{lockObtainedAt.Ticks}
            var parts = @lock.LockId!.Split(':');
            Assert.Equal(4, parts.Length);
            Assert.Equal("test-provider", parts[0]);
            Assert.Equal("test-lock", parts[1]);
            Assert.Equal("100", parts[2]);
        }

        #endregion

        #region CloudDistributedLockProvider Tests

        [Fact]
        public async Task TryAcquireLockAsync_WhenLockAvailable_ReturnsAcquiredLock()
        {
            var cosmosLockClient = Substitute.For<ICosmosLockClient>();
            var item = CreateMockItemResponse(CreateLockRecord());
            cosmosLockClient.TryAcquireLockAsync("test-lock").Returns(item);
            cosmosLockClient.RenewLockAsync(Arg.Any<ItemResponse<LockRecord>>()).Returns((ItemResponse<LockRecord>?)null);
            cosmosLockClient.ReleaseLockAsync(Arg.Any<ItemResponse<LockRecord>>()).Returns(Task.CompletedTask);

            var options = new CloudDistributedLockProviderOptions();
            var provider = new CloudDistributedLockProvider(cosmosLockClient, options);

            using var @lock = await provider.TryAcquireLockAsync("test-lock");

            Assert.True(@lock.IsAcquired);
        }

        [Fact]
        public async Task TryAcquireLockAsync_WhenLockUnavailable_ReturnsUnacquiredLock()
        {
            var cosmosLockClient = Substitute.For<ICosmosLockClient>();
            cosmosLockClient.TryAcquireLockAsync("test-lock").Returns((ItemResponse<LockRecord>?)null);

            var options = new CloudDistributedLockProviderOptions();
            var provider = new CloudDistributedLockProvider(cosmosLockClient, options);

            using var @lock = await provider.TryAcquireLockAsync("test-lock");

            Assert.False(@lock.IsAcquired);
        }

        [Fact]
        public async Task AcquireLockAsync_WithTimeout_WhenLockUnavailable_ReturnsUnacquiredLock()
        {
            var cosmosLockClient = Substitute.For<ICosmosLockClient>();
            cosmosLockClient.TryAcquireLockAsync("test-lock").Returns((ItemResponse<LockRecord>?)null);

            var options = new CloudDistributedLockProviderOptions { RetryInterval = TimeSpan.FromMilliseconds(50) };
            var provider = new CloudDistributedLockProvider(cosmosLockClient, options);

            using var @lock = await provider.AcquireLockAsync("test-lock", TimeSpan.FromMilliseconds(200));

            Assert.False(@lock.IsAcquired);
        }

        [Fact]
        public async Task AcquireLockAsync_WithTimeout_RetriesUntilLockAvailable()
        {
            var attemptCount = 0;
            var cosmosLockClient = Substitute.For<ICosmosLockClient>();
            var item = CreateMockItemResponse(CreateLockRecord());
            cosmosLockClient.TryAcquireLockAsync("test-lock").Returns(_ =>
            {
                if (Interlocked.Increment(ref attemptCount) >= 3)
                    return item;
                return (ItemResponse<LockRecord>?)null;
            });
            cosmosLockClient.RenewLockAsync(Arg.Any<ItemResponse<LockRecord>>()).Returns((ItemResponse<LockRecord>?)null);
            cosmosLockClient.ReleaseLockAsync(Arg.Any<ItemResponse<LockRecord>>()).Returns(Task.CompletedTask);

            var options = new CloudDistributedLockProviderOptions { RetryInterval = TimeSpan.FromMilliseconds(50) };
            var provider = new CloudDistributedLockProvider(cosmosLockClient, options);

            using var @lock = await provider.AcquireLockAsync("test-lock", TimeSpan.FromSeconds(5));

            Assert.True(@lock.IsAcquired);
            Assert.True(attemptCount >= 3);
        }

        [Fact]
        public async Task AcquireLockAsync_FirstAttemptSucceeds_ReturnsImmediately()
        {
            var cosmosLockClient = Substitute.For<ICosmosLockClient>();
            var item = CreateMockItemResponse(CreateLockRecord());
            cosmosLockClient.TryAcquireLockAsync("test-lock").Returns(item);
            cosmosLockClient.RenewLockAsync(Arg.Any<ItemResponse<LockRecord>>()).Returns((ItemResponse<LockRecord>?)null);
            cosmosLockClient.ReleaseLockAsync(Arg.Any<ItemResponse<LockRecord>>()).Returns(Task.CompletedTask);

            var options = new CloudDistributedLockProviderOptions { RetryInterval = TimeSpan.FromMilliseconds(50) };
            var provider = new CloudDistributedLockProvider(cosmosLockClient, options);

            using var @lock = await provider.AcquireLockAsync("test-lock", TimeSpan.FromSeconds(5));

            Assert.True(@lock.IsAcquired);
            await cosmosLockClient.Received(1).TryAcquireLockAsync("test-lock");
        }

        [Fact]
        public async Task AcquireLockAsync_WithoutTimeout_AcquiresWhenAvailable()
        {
            var attemptCount = 0;
            var cosmosLockClient = Substitute.For<ICosmosLockClient>();
            var item = CreateMockItemResponse(CreateLockRecord());
            cosmosLockClient.TryAcquireLockAsync("test-lock").Returns(_ =>
            {
                if (Interlocked.Increment(ref attemptCount) >= 2)
                    return item;
                return (ItemResponse<LockRecord>?)null;
            });
            cosmosLockClient.RenewLockAsync(Arg.Any<ItemResponse<LockRecord>>()).Returns((ItemResponse<LockRecord>?)null);
            cosmosLockClient.ReleaseLockAsync(Arg.Any<ItemResponse<LockRecord>>()).Returns(Task.CompletedTask);

            var options = new CloudDistributedLockProviderOptions { RetryInterval = TimeSpan.FromMilliseconds(50) };
            var provider = new CloudDistributedLockProvider(cosmosLockClient, options);

            // AcquireLockAsync with no timeout should still eventually return when lock becomes available
            using var @lock = await provider.AcquireLockAsync("test-lock");

            Assert.True(@lock.IsAcquired);
            Assert.True(attemptCount >= 2);
        }

        #endregion
    }
}
