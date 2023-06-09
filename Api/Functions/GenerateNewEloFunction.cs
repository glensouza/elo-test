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
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;
using HtmlAgilityPack;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using Api.Helpers;
using Api.Queues;

namespace Api.Functions;

public class GenerateNewEloFunction
{
    private readonly ILogger logger;
    private readonly HttpClient httpClient;
    private readonly CarNameGenerator carNameGenerator;
    private readonly HtmlDocument htmlDoc;
    private readonly NewPictureQueue newPictureQueue;
    private readonly PictureTable pictureTable;
    private readonly BlobContainerClient blobContainerClient;
    private const string CarDoesNotExistUrl = "https://www.thisautomobiledoesnotexist.com/";

    public GenerateNewEloFunction(
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory,
        CarNameGenerator carNameGenerator,
        NewPictureQueue newPictureQueue,
        PictureTable pictureTable,
        BlobContainerClient blobClient)
    {
        this.logger = loggerFactory.CreateLogger<GenerateNewEloFunction>();
        this.httpClient = httpClientFactory.CreateClient();
        this.carNameGenerator = carNameGenerator;
        this.htmlDoc = new HtmlDocument();
        this.newPictureQueue = newPictureQueue;
        this.pictureTable = pictureTable;
        this.blobContainerClient = blobClient;
    }

    [Function("GenerateNewElo")]
    public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        this.logger.LogInformation("C# HTTP trigger function processed a request.");

        // generate 10 Pictures
        for (int i = 0; i < 10; i++)
        {
            string carName;
            List<PictureEntity> queryUniquePictureEntities;
            do
            {
                // verify uniqueness of name
                carName = this.carNameGenerator.GetRandomCarName();
                string name = carName;
                queryUniquePictureEntities = this.pictureTable.GetPictureEntitiesByName(name);
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

            PictureEntity? existingPictureEntity = this.pictureTable.GetPictureEntityByRowKey(pictureEntity.RowKey);
            while (existingPictureEntity != null)
            {
                pictureEntity.RowKey = Guid.NewGuid().ToString();
                existingPictureEntity = this.pictureTable.GetPictureEntityByRowKey(pictureEntity.RowKey);
            }

            BlobClient? bigPictureCloudBlockBlob = this.blobContainerClient.GetBlobClient($"{pictureEntity.RowKey}.png");
            bool fileExists = await bigPictureCloudBlockBlob.ExistsAsync();
            while (fileExists)
            {
                await bigPictureCloudBlockBlob.DeleteAsync();
                fileExists = await bigPictureCloudBlockBlob.ExistsAsync();
            }

            BlobClient? smallPictureCloudBlockBlob = this.blobContainerClient.GetBlobClient($"{pictureEntity.RowKey}_sml.png");
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
                this.logger.LogInformation("SAS URI for blob is: {0}", sasUri);

                picUri = sasUri.AbsoluteUri;
            }
            else
            {
                this.logger.LogError("BlobClient must be authorized with Shared Key credentials to create a service SAS.");
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
                this.logger.LogInformation("SAS URI for blob is: {0}", sasUri);

                smallPicUri = sasUri.AbsoluteUri;
            }
            else
            {
                this.logger.LogError("BlobClient must be authorized with Shared Key credentials to create a service SAS.");
            }

            pictureEntity.PictureUri = picUri;
            pictureEntity.PictureSmlUri = smallPicUri;
            await this.pictureTable.AddPictureEntityAsync(pictureEntity);
            await this.newPictureQueue.SendMessageAsync(pictureEntity.RowKey);
        }

        HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
        return response;
    }
}
