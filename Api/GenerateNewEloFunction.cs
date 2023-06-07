using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;
using HtmlAgilityPack;

namespace Api;

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
    public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        this.logger.LogInformation("C# HTTP trigger function processed a request.");

        // generate 10 Pictures
        // TODO: VERIFY
        for (int i = 0; i < 10; i++)
        {
            string carName;
            Pageable<PictureEntity> queryUniquePictureEntities;
            do
            {
                // verify uniqueness of name
                carName = this.carNameGenerator.GetRandomCarName();
                string name = carName;
                queryUniquePictureEntities = this.pictureTableClient.Query<PictureEntity>( s => s.Name == name, 1);
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
            PictureEntity pictureEntity = new() { Name = carName }; 

            NullableResponse<PictureEntity> existingPictureEntity = await this.pictureTableClient.GetEntityIfExistsAsync<PictureEntity>(pictureEntity.PartitionKey, pictureEntity.RowKey);
            while (existingPictureEntity.HasValue)
            {
                pictureEntity.RowKey = Guid.NewGuid().ToString();
                existingPictureEntity = await this.pictureTableClient.GetEntityIfExistsAsync<PictureEntity>(pictureEntity.PartitionKey, pictureEntity.RowKey);
            }

            BlobClient? cloudBlockBlob = this.blobContainerClient.GetBlobClient($"{pictureEntity.RowKey}.png");
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
            }

            pictureEntity.PictureUri = uri;
            await this.pictureTableClient.AddEntityAsync(pictureEntity);

            // get all pictures from table
            Pageable<PictureEntity> allPicturesQuery = this.pictureTableClient.Query<PictureEntity>();
            List<PictureEntity> allPictures = allPicturesQuery.AsPages().SelectMany(page => page.Values).ToList();

            // exclude eloEntity from allPictures
            pictureEntity = allPictures.FirstOrDefault(s => s.RowKey == pictureEntity.RowKey)!;
            allPictures.Remove(pictureEntity);

            if (allPictures.Count <= 0)
            {
                continue;
            }

            // enter all competitions for this picture
            foreach (string pictureId in allPictures.Select(s => s.RowKey))
            {
                NullableResponse<EloEntity> existingEloEntity = await this.eloTableClient.GetEntityIfExistsAsync<EloEntity>(pictureEntity.PartitionKey, pictureId);
                if(existingEloEntity.HasValue)
                {
                    existingEloEntity.Value.Won = null;
                    await this.eloTableClient.UpdateEntityAsync(existingEloEntity.Value, ETag.All, TableUpdateMode.Replace);
                }
                else
                {
                    await this.eloTableClient.AddEntityAsync(new EloEntity { PartitionKey = pictureEntity.RowKey, RowKey = pictureId, Won = null });
                }

                existingEloEntity = await this.eloTableClient.GetEntityIfExistsAsync<EloEntity>(pictureId, pictureEntity.PartitionKey);
                if (existingEloEntity.HasValue)
                {
                    existingEloEntity.Value.Won = null;
                    await this.eloTableClient.UpdateEntityAsync(existingEloEntity.Value, ETag.All, TableUpdateMode.Replace);
                }
                else
                {
                    await this.eloTableClient.AddEntityAsync(new EloEntity { PartitionKey = pictureId, RowKey = pictureEntity.RowKey, Won = null });
                }
            }
        }

        HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
        return response;
    }
}
