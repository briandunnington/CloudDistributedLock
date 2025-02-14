using Element.CloudDistributedLock;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace ExampleApp
{
    public class Functions
    {
        public const string LockName = "MyLock";
        public const string OtherLockName = "OtherLockName";

        private readonly ICloudDistributedLockProviderFactory lockProviderFactory;

        public Functions(ICloudDistributedLockProviderFactory lockProviderFactory)
        {
            this.lockProviderFactory = lockProviderFactory;
        }

        [Function("TryLock")]
        public async Task<HttpResponseData> TryLock([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            var response = req.CreateResponse();
            var lockProvider = lockProviderFactory.GetLockProvider();
            using var @lock = await lockProvider.TryAcquireLockAsync(LockName);
            if (@lock.IsAcquired)
            {
                response.StatusCode = HttpStatusCode.OK;
                await response.WriteAsJsonAsync(new
                {
                    message = "TryLock obtained the lock",
                    lockId = @lock.LockId,
                    etag = @lock.ETag,
                    fencingToken = @lock.FencingToken
                });

                // hold the lock for 10 seconds to demonstrate
                await Task.Delay(TimeSpan.FromSeconds(10));
            }
            else
            {
                response.StatusCode = HttpStatusCode.Conflict;
                response.WriteString("TryLock failed to obtain the lock immediately");
            }
            return response;
        }

        [Function("OtherLock")]
        public async Task<HttpResponseData> OtherLock([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            var response = req.CreateResponse();
            var lockProvider = lockProviderFactory.GetLockProvider();
            using var @lock = await lockProvider.TryAcquireLockAsync(OtherLockName);
            if (@lock.IsAcquired)
            {
                response.StatusCode = HttpStatusCode.OK;
                await response.WriteAsJsonAsync(new
                {
                    message = "OtherLock obtained the lock",
                    lockId = @lock.LockId,
                    etag = @lock.ETag,
                    fencingToken = @lock.FencingToken
                });
            }
            else
            {
                response.StatusCode = HttpStatusCode.Conflict;
                response.WriteString("OtherLock failed to obtain the lock immediately");
            }
            return response;
        }

        [Function("WaitLock")]
        public async Task<HttpResponseData> WaitLock([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            var lockProvider = lockProviderFactory.GetLockProvider();
            using var @lock = await lockProvider.AcquireLockAsync(LockName);
            if (@lock.IsAcquired)
            {
                response.StatusCode = HttpStatusCode.OK;
                await response.WriteAsJsonAsync(new
                {
                    message = "WaitLock obtained the lock",
                    lockId = @lock.LockId,
                    etag = @lock.ETag,
                    fencingToken = @lock.FencingToken
                });
            }
            else
            {
                // since no timeout was specified in the AcquireAsync(), this should not get hit
                response.StatusCode = HttpStatusCode.Conflict;
                response.WriteString("WaitLock failed to obtain the lock - should not happen");
            }
            return response;
        }

        [Function("WaitTimeoutLock")]
        public async Task<HttpResponseData> WaitTimeoutLock([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            var lockProvider = lockProviderFactory.GetLockProvider();
            using var @lock = await lockProvider.AcquireLockAsync(LockName, TimeSpan.FromSeconds(2));
            if (@lock.IsAcquired)
            {
                response.StatusCode = HttpStatusCode.OK;
                await response.WriteAsJsonAsync(new
                {
                    message = "WaitTimeoutLock obtained the lock",
                    lockId = @lock.LockId,
                    etag = @lock.ETag,
                    fencingToken = @lock.FencingToken
                });
            }
            else
            {
                // this will get hit if the lock can not be acquired in 2 seconds
                response.StatusCode = HttpStatusCode.Conflict;
                response.WriteString("WaitTimeoutLock failed to obtain a lock within 2 seconds");
            }
            return response;
        }
    }
}
