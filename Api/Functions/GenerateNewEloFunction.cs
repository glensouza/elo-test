using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Api.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;
using HtmlAgilityPack;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using Api.Helpers;

namespace Api.Functions;

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
        logger = loggerFactory.CreateLogger<GenerateNewEloFunction>();
        httpClient = httpClientFactory.CreateClient();
        this.carNameGenerator = carNameGenerator;
        htmlDoc = new HtmlDocument();
        eloTableClient = eloTable.Client;
        pictureTableClient = pictureTable.Client;
        blobContainerClient = blobClient;
    }

    [Function("GenerateNewElo")]
    public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        logger.LogInformation("C# HTTP trigger function processed a request.");

        // generate 10 Pictures
        for (int i = 0; i < 10; i++)
        {
            string carName;
            Pageable<PictureEntity> queryUniquePictureEntities;
            do
            {
                // verify uniqueness of name
                carName = carNameGenerator.GetRandomCarName();
                string name = carName;
                queryUniquePictureEntities = pictureTableClient.Query<PictureEntity>(s => s.Name == name, 1);
            } while (queryUniquePictureEntities.Any());

            // go get a car image
            string carDoesNotExistHtml = await httpClient.GetStringAsync(CarDoesNotExistUrl);
            if (string.IsNullOrEmpty(carDoesNotExistHtml))
            {
                logger.LogError("website down!");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }

            htmlDoc.LoadHtml(carDoesNotExistHtml);

            // Check if the image exists
            HtmlNode? imgNode = htmlDoc.DocumentNode.SelectSingleNode("//img[@id='vehicle']");
            if (imgNode == null)
            {
                logger.LogError("website down!");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }

            string src = imgNode.GetAttributeValue("src", "");
            PictureEntity pictureEntity = new() { Name = carName };

            NullableResponse<PictureEntity> existingPictureEntity = await pictureTableClient.GetEntityIfExistsAsync<PictureEntity>(pictureEntity.PartitionKey, pictureEntity.RowKey);
            while (existingPictureEntity.HasValue)
            {
                pictureEntity.RowKey = Guid.NewGuid().ToString();
                existingPictureEntity = await pictureTableClient.GetEntityIfExistsAsync<PictureEntity>(pictureEntity.PartitionKey, pictureEntity.RowKey);
            }

            BlobClient? bigPictureCloudBlockBlob = blobContainerClient.GetBlobClient($"{pictureEntity.RowKey}.png");
            bool fileExists = await bigPictureCloudBlockBlob.ExistsAsync();
            while (fileExists)
            {
                await bigPictureCloudBlockBlob.DeleteAsync();
                fileExists = await bigPictureCloudBlockBlob.ExistsAsync();
            }

            BlobClient? smallPictureCloudBlockBlob = blobContainerClient.GetBlobClient($"{pictureEntity.RowKey}_sml.png");
            fileExists = await smallPictureCloudBlockBlob.ExistsAsync();
            while (fileExists)
            {
                await smallPictureCloudBlockBlob.DeleteAsync();
                fileExists = await smallPictureCloudBlockBlob.ExistsAsync();
            }

            byte[] data = Convert.FromBase64String(src.Replace("data:image/png;base64,", ""));
            using MemoryStream stream = new(data, 0, data.Length);
            await bigPictureCloudBlockBlob.UploadAsync(stream);

            string picUri = bigPictureCloudBlockBlob.Uri.AbsoluteUri;
            if (bigPictureCloudBlockBlob.CanGenerateSasUri)
            {
                // Create a SAS token that's valid for one year
                BlobSasBuilder sasBuilder = new()
                {
                    BlobContainerName = bigPictureCloudBlockBlob.GetParentBlobContainerClient().Name,
                    BlobName = bigPictureCloudBlockBlob.Name,
                    Resource = "b",
                    ExpiresOn = DateTimeOffset.UtcNow.AddYears(1)
                };

                sasBuilder.SetPermissions(BlobSasPermissions.Read);

                Uri sasUri = bigPictureCloudBlockBlob.GenerateSasUri(sasBuilder);
                logger.LogInformation("SAS URI for blob is: {0}", sasUri);

                picUri = sasUri.AbsoluteUri;
            }
            else
            {
                logger.LogError("BlobClient must be authorized with Shared Key credentials to create a service SAS.");
            }

            // Resize the image to 20x20
            using Image image = Image.Load(data);
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(20, 20),
                Mode = ResizeMode.Max
            }));
            using MemoryStream smallStream = new();
            await image.SaveAsync(smallStream, new PngEncoder());
            smallStream.Position = 0;
            await smallPictureCloudBlockBlob.UploadAsync(smallStream);

            string smallPicUri = smallPictureCloudBlockBlob.Uri.AbsoluteUri;
            if (smallPictureCloudBlockBlob.CanGenerateSasUri)
            {
                // Create a SAS token that's valid for one year
                BlobSasBuilder sasBuilder = new()
                {
                    BlobContainerName = smallPictureCloudBlockBlob.GetParentBlobContainerClient().Name,
                    BlobName = smallPictureCloudBlockBlob.Name,
                    Resource = "b",
                    ExpiresOn = DateTimeOffset.UtcNow.AddYears(1)
                };

                sasBuilder.SetPermissions(BlobSasPermissions.Read);

                Uri sasUri = smallPictureCloudBlockBlob.GenerateSasUri(sasBuilder);
                logger.LogInformation("SAS URI for blob is: {0}", sasUri);

                smallPicUri = sasUri.AbsoluteUri;
            }
            else
            {
                logger.LogError("BlobClient must be authorized with Shared Key credentials to create a service SAS.");
            }

            pictureEntity.PictureUri = picUri;
            pictureEntity.PictureSmlUri = smallPicUri;
            await pictureTableClient.AddEntityAsync(pictureEntity);

            // get all pictures from table
            Pageable<PictureEntity> allPicturesQuery = pictureTableClient.Query<PictureEntity>();
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
                NullableResponse<EloEntity> existingEloEntity = await eloTableClient.GetEntityIfExistsAsync<EloEntity>(pictureEntity.PartitionKey, pictureId);
                if (existingEloEntity.HasValue)
                {
                    existingEloEntity.Value.Won = null;
                    await eloTableClient.UpdateEntityAsync(existingEloEntity.Value, ETag.All, TableUpdateMode.Replace);
                }
                else
                {
                    await eloTableClient.AddEntityAsync(new EloEntity { PartitionKey = pictureEntity.RowKey, RowKey = pictureId, Won = null });
                }

                existingEloEntity = await eloTableClient.GetEntityIfExistsAsync<EloEntity>(pictureId, pictureEntity.PartitionKey);
                if (existingEloEntity.HasValue)
                {
                    existingEloEntity.Value.Won = null;
                    await eloTableClient.UpdateEntityAsync(existingEloEntity.Value, ETag.All, TableUpdateMode.Replace);
                }
                else
                {
                    await eloTableClient.AddEntityAsync(new EloEntity { PartitionKey = pictureId, RowKey = pictureEntity.RowKey, Won = null });
                }
            }
        }

        HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
        return response;
    }
}
