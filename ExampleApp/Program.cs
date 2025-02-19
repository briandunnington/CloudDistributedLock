using Element.CloudDistributedLock;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((hbc, services) =>
    {
        var config = hbc.Configuration;
        services.AddCloudDistributedLock(config["CosmosEndpoint"], config["CosmosKey"], config["DatabaseName"], 5);

        // Use this if you want the internal CosmosClient used by the locking library to use managed identity instead of a key
        //services.AddCloudDistributedLock(new CosmosClient(config["CosmosEndpoint"], new DefaultAzureCredential()), config["DatabaseName"], 5);
    })
    .Build();

host.Run();
