using Api;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

using Azure.Data.Tables;
using Azure.Storage.Blobs;
using HtmlAgilityPack;
using Microsoft.Extensions.DependencyInjection;

IConfiguration config = new ConfigurationBuilder()
    .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

string storageConnectionString = config.GetValue<string>("AzureWebJobsStorage");

IHost host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(s =>
    {
        s.AddHttpClient();
        s.AddSingleton(_ => new CarNameGenerator());
        s.AddSingleton(_ => new EloTable(storageConnectionString));
        s.AddSingleton(_ => new PictureTable(storageConnectionString));
        s.AddSingleton(_ =>
        {
            BlobContainerClient container = new(storageConnectionString, "elo");
            container.CreateIfNotExists();
            return container;
        });
    })
    .Build();

await host.RunAsync();
