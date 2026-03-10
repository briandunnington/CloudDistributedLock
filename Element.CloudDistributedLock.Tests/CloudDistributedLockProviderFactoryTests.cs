using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Element.CloudDistributedLock.Tests
{
    public class CloudDistributedLockProviderFactoryTests
    {
        private static IOptionsMonitor<CloudDistributedLockProviderOptions> CreateOptionsMonitor(
            string name,
            CloudDistributedLockProviderOptions options)
        {
            var monitor = Substitute.For<IOptionsMonitor<CloudDistributedLockProviderOptions>>();
            monitor.Get(name).Returns(options);
            return monitor;
        }

        private static CloudDistributedLockProviderOptions CreateValidOptions()
        {
            return new CloudDistributedLockProviderOptions
            {
                DatabaseName = "testdb",
                ContainerName = "locks",
                ProviderName = "test-provider",
                CosmosClient = Substitute.For<CosmosClient>()
            };
        }

        [Fact]
        public void GetLockProvider_Default_ReturnsProvider()
        {
            var options = CreateValidOptions();
            var monitor = CreateOptionsMonitor(CloudDistributedLockProviderFactory.DefaultName, options);

            var factory = new CloudDistributedLockProviderFactory(monitor);
            var provider = factory.GetLockProvider();

            Assert.NotNull(provider);
        }

        [Fact]
        public void GetLockProvider_Named_ReturnsProvider()
        {
            var options = CreateValidOptions();
            var monitor = CreateOptionsMonitor("my-provider", options);

            var factory = new CloudDistributedLockProviderFactory(monitor);
            var provider = factory.GetLockProvider("my-provider");

            Assert.NotNull(provider);
        }

        [Fact]
        public void GetLockProvider_CalledTwice_ReturnsSameInstance()
        {
            var options = CreateValidOptions();
            var monitor = CreateOptionsMonitor("my-provider", options);

            var factory = new CloudDistributedLockProviderFactory(monitor);
            var provider1 = factory.GetLockProvider("my-provider");
            var provider2 = factory.GetLockProvider("my-provider");

            Assert.Same(provider1, provider2);
        }

        [Fact]
        public void GetLockProvider_DifferentNames_ReturnsDifferentInstances()
        {
            var options1 = CreateValidOptions();
            var options2 = CreateValidOptions();
            var monitor = Substitute.For<IOptionsMonitor<CloudDistributedLockProviderOptions>>();
            monitor.Get("provider-a").Returns(options1);
            monitor.Get("provider-b").Returns(options2);

            var factory = new CloudDistributedLockProviderFactory(monitor);
            var providerA = factory.GetLockProvider("provider-a");
            var providerB = factory.GetLockProvider("provider-b");

            Assert.NotSame(providerA, providerB);
        }

        [Fact]
        public void GetLockProvider_NullProviderName_ThrowsArgumentNullException()
        {
            var options = CreateValidOptions();
            options.ProviderName = null;
            var monitor = CreateOptionsMonitor("test", options);

            var factory = new CloudDistributedLockProviderFactory(monitor);

            Assert.Throws<ArgumentNullException>(() => factory.GetLockProvider("test"));
        }

        [Fact]
        public void GetLockProvider_NullCosmosClient_ThrowsArgumentNullException()
        {
            var options = CreateValidOptions();
            options.CosmosClient = null;
            var monitor = CreateOptionsMonitor("test", options);

            var factory = new CloudDistributedLockProviderFactory(monitor);

            Assert.Throws<ArgumentNullException>(() => factory.GetLockProvider("test"));
        }

        [Fact]
        public void GetLockProvider_NullDatabaseName_ThrowsArgumentNullException()
        {
            var options = CreateValidOptions();
            options.DatabaseName = null;
            var monitor = CreateOptionsMonitor("test", options);

            var factory = new CloudDistributedLockProviderFactory(monitor);

            Assert.Throws<ArgumentNullException>(() => factory.GetLockProvider("test"));
        }

        [Fact]
        public void GetLockProvider_NullContainerName_ThrowsArgumentNullException()
        {
            var options = CreateValidOptions();
            options.ContainerName = null!;
            var monitor = CreateOptionsMonitor("test", options);

            var factory = new CloudDistributedLockProviderFactory(monitor);

            Assert.Throws<ArgumentNullException>(() => factory.GetLockProvider("test"));
        }
    }
}
