using Element.CloudDistributedLock;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((hbc, services) =>
    {
        var config = hbc.Configuration;
        services.AddCloudDistributedLock(config["CosmosEndpoint"], config["CosmosKey"], config["DatabaseName"], 5);
    })
    .Build();

host.Run();
