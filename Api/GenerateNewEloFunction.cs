using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using HtmlAgilityPack;
using System.Xml;
using System;
using static System.Net.Mime.MediaTypeNames;
using System.IO;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;

namespace Api
{
    public class GenerateNewEloFunction
    {
        private readonly ILogger logger;
        private readonly HttpClient httpClient;
        private readonly CarNameGenerator carNameGenerator;
        private readonly HtmlDocument htmlDoc;
        private readonly TableClient tableClient;
        private readonly BlobContainerClient blobContainerClient;
        private const string CarDoesNotExistUrl = "https://www.thisautomobiledoesnotexist.com/";

        public GenerateNewEloFunction(ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory, CarNameGenerator carNameGenerator, TableClient tableClient, BlobContainerClient blobClient)
        {
            this.logger = loggerFactory.CreateLogger<GenerateNewEloFunction>();
            this.httpClient = httpClientFactory.CreateClient();
            this.carNameGenerator = carNameGenerator;
            this.htmlDoc = new HtmlDocument();
            this.tableClient = tableClient;
            this.blobContainerClient = blobClient;
        }

        [Function("GenerateNewElo")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
        {
            this.logger.LogInformation("C# HTTP trigger function processed a request.");

            // TODO: verify uniqueness of name
            string carName = this.carNameGenerator.GetRandomCarName();

            string carDoesNotExistHtml = await this.httpClient.GetStringAsync(CarDoesNotExistUrl);
            if (string.IsNullOrEmpty(carDoesNotExistHtml))
            {
                this.logger.LogError("website down!");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }






            this.htmlDoc.LoadHtml(carDoesNotExistHtml);

            HtmlNode? imgNode = this.htmlDoc.DocumentNode.SelectSingleNode("//img[@id='vehicle']");
            // Check if the node exists
            if (imgNode == null)
            {
                this.logger.LogError("website down!");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }

            // Do something with the image node
            string src = imgNode.GetAttributeValue("src", "");


            PictureEntity eloEntity = new() { Name = carName }; 

            NullableResponse<PictureEntity> existingEloEntity = await this.tableClient.GetEntityIfExistsAsync<PictureEntity>(eloEntity.PartitionKey, eloEntity.RowKey);
            while (existingEloEntity.HasValue)
            {
                eloEntity.RowKey = Guid.NewGuid().ToString();
                existingEloEntity = await this.tableClient.GetEntityIfExistsAsync<PictureEntity>(eloEntity.PartitionKey, eloEntity.RowKey);
            }

            BlobClient? cloudBlockBlob = this.blobContainerClient.GetBlobClient($"{eloEntity.RowKey}.png");
            bool fileExists = await cloudBlockBlob.ExistsAsync();
            while (fileExists)
            {
                await cloudBlockBlob.DeleteAsync();
                fileExists = await cloudBlockBlob.ExistsAsync();
            }

            byte[] data = Convert.FromBase64String(src.Replace("data:image/png;base64,", ""));
            using (MemoryStream stream = new(data, 0, data.Length))
            {
                await cloudBlockBlob.UploadAsync(stream);
            }

            string uri = cloudBlockBlob.Uri.AbsoluteUri;
            if (cloudBlockBlob.CanGenerateSasUri)
            {
                // Create a SAS token that's valid for one hour.
                BlobSasBuilder sasBuilder = new()
                {
                    BlobContainerName = cloudBlockBlob.GetParentBlobContainerClient().Name,
                    BlobName = cloudBlockBlob.Name,
                    Resource = "b",
                    ExpiresOn = DateTimeOffset.UtcNow.AddYears(1)
                };

                sasBuilder.SetPermissions(BlobSasPermissions.Read);

                Uri sasUri = cloudBlockBlob.GenerateSasUri(sasBuilder);
                this.logger.LogInformation("SAS URI for blob is: {0}", sasUri);

                uri = sasUri.AbsoluteUri;
            }
            else
            {
                this.logger.LogError("BlobClient must be authorized with Shared Key credentials to create a service SAS.");
                //return new ExceptionResult(new Exception("BlobClient must be authorized with Shared Key credentials to create a service SAS."), false);
            }

            eloEntity.PictureUri = uri;
            await this.tableClient.AddEntityAsync(eloEntity);

            HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/html; charset=utf-8");

            await response.WriteStringAsync($"<html><body><h1>{carName}</h1><br /><img src='{src}' /></body></html");

            return response;
        }
    }
}
