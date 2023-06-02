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
        private readonly TableClient eloTableClient;
        private readonly TableClient pictureTableClient;
        private readonly BlobContainerClient blobContainerClient;
        private const string CarDoesNotExistUrl = "https://www.thisautomobiledoesnotexist.com/";

        public GenerateNewEloFunction(
            ILoggerFactory loggerFactory, 
            IHttpClientFactory httpClientFactory, 
            CarNameGenerator carNameGenerator, 
            EloTable eloTable,
            PictureTable pictureTable,
            BlobContainerClient blobClient)
        {
            this.logger = loggerFactory.CreateLogger<GenerateNewEloFunction>();
            this.httpClient = httpClientFactory.CreateClient();
            this.carNameGenerator = carNameGenerator;
            this.htmlDoc = new HtmlDocument();
            this.eloTableClient = eloTable.Client;
            this.pictureTableClient = pictureTable.Client;
            this.blobContainerClient = blobClient;
        }

        [Function("GenerateNewElo")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
        {
            this.logger.LogInformation("C# HTTP trigger function processed a request.");

            string carName;
            Pageable<TableEntity> queryUniquePictureEntities;
            do
            {
                // verify uniqueness of name
                carName = this.carNameGenerator.GetRandomCarName();
                queryUniquePictureEntities = this.pictureTableClient.Query<TableEntity>($"{nameof(PictureEntity.Name)} eq '{carName}'", 1);
            } while (queryUniquePictureEntities.Any());

            // go get a car image
            string carDoesNotExistHtml = await this.httpClient.GetStringAsync(CarDoesNotExistUrl);
            if (string.IsNullOrEmpty(carDoesNotExistHtml))
            {
                this.logger.LogError("website down!");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }

            this.htmlDoc.LoadHtml(carDoesNotExistHtml);

            // Check if the image exists
            HtmlNode? imgNode = this.htmlDoc.DocumentNode.SelectSingleNode("//img[@id='vehicle']");
            if (imgNode == null)
            {
                this.logger.LogError("website down!");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }

            string src = imgNode.GetAttributeValue("src", "");
            PictureEntity eloEntity = new() { Name = carName }; 

            NullableResponse<PictureEntity> existingPictureEntity = await this.pictureTableClient.GetEntityIfExistsAsync<PictureEntity>(eloEntity.PartitionKey, eloEntity.RowKey);
            while (existingPictureEntity.HasValue)
            {
                eloEntity.RowKey = Guid.NewGuid().ToString();
                existingPictureEntity = await this.pictureTableClient.GetEntityIfExistsAsync<PictureEntity>(eloEntity.PartitionKey, eloEntity.RowKey);
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
                // Create a SAS token that's valid for one year
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
            await this.pictureTableClient.AddEntityAsync(eloEntity);

            // TODO: Add all elo competition entries

            // get all pictures from table
            Pageable<PictureEntity> allPicturesQuery = this.pictureTableClient.Query<PictureEntity>();
            List<PictureEntity> allPictures = allPicturesQuery.AsPages().SelectMany(page => page.Values).ToList();

            // exclude eloEntity from allPictures
            eloEntity = allPictures.FirstOrDefault(s => s.RowKey == eloEntity.RowKey)!;
            allPictures.Remove(eloEntity);

            if (allPictures.Count > 0)
            {
                foreach (string pictureId in allPictures.Select(s => s.RowKey))
                {
                    NullableResponse<EloEntity> existingEloEntity = await this.eloTableClient.GetEntityIfExistsAsync<EloEntity>(eloEntity.PartitionKey, pictureId);
                    if(existingEloEntity.HasValue)
                    {
                        existingEloEntity.Value.Won = null;
                        await this.eloTableClient.UpdateEntityAsync(existingEloEntity.Value, ETag.All, TableUpdateMode.Replace);
                    }
                    else
                    {
                        await this.eloTableClient.AddEntityAsync(new EloEntity { PartitionKey = eloEntity.RowKey, RowKey = pictureId, Won = null });
                    }

                    existingEloEntity = await this.eloTableClient.GetEntityIfExistsAsync<EloEntity>(pictureId, eloEntity.PartitionKey);
                    if (existingEloEntity.HasValue)
                    {
                        existingEloEntity.Value.Won = null;
                        await this.eloTableClient.UpdateEntityAsync(existingEloEntity.Value, ETag.All, TableUpdateMode.Replace);
                    }
                    else
                    {
                        await this.eloTableClient.AddEntityAsync(new EloEntity { PartitionKey = pictureId, RowKey = eloEntity.RowKey, Won = null });
                    }
                }
            }

            HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);

            // return this while testing:
            response.Headers.Add("Content-Type", "text/html; charset=utf-8");
            await response.WriteStringAsync($"<html><body><h1>{carName}</h1><br /><img src='{src}' /></body></html");

            return response;
        }
    }
}
