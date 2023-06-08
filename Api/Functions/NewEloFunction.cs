using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Api.Entities;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;
using BlazorApp.Shared;
using HttpMultipartParser;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace Api.Functions;

public class NewEloFunction
{
    private readonly ILogger logger;
    private readonly TableClient pictureTableClient;
    private readonly TableClient eloTableClient;
    private readonly BlobContainerClient blobContainerClient;

    public NewEloFunction(ILoggerFactory loggerFactory, PictureTable pictureTable, EloTable eloTable, BlobContainerClient blobClient)
    {
        logger = loggerFactory.CreateLogger<NewEloFunction>();
        pictureTableClient = pictureTable.Client;
        eloTableClient = eloTable.Client;
        blobContainerClient = blobClient;
    }

    [Function("NewElo")]
    public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "put")] HttpRequestData req)
    {
        logger.LogInformation("C# HTTP trigger function processed a request.");

        List<UploadResult> uploadResults = new();

        // get form-body
        MultipartFormDataParser parsedFormBody = await MultipartFormDataParser.ParseAsync(req.Body);
        if (parsedFormBody.Files.Count == 0)
        {
            HttpResponseData badRequestResponse = req.CreateResponse(HttpStatusCode.OK);
            await badRequestResponse.WriteAsJsonAsync(uploadResults);
            return badRequestResponse;
        }

        foreach (FilePart filePart in parsedFormBody.Files)
        {
            string filename = filePart.FileName;
            Stream stream = filePart.Data;

            PictureEntity pictureEntity = new()
            {
                Name = parsedFormBody.HasParameter("name") ? parsedFormBody.GetParameterValues("name").First() : filename.Replace($"{Path.GetExtension(filename)}", string.Empty)
            };

            NullableResponse<PictureEntity> existingPictureEntity = await pictureTableClient.GetEntityIfExistsAsync<PictureEntity>(pictureEntity.PartitionKey, pictureEntity.RowKey);
            while (existingPictureEntity.HasValue)
            {
                pictureEntity.RowKey = Guid.NewGuid().ToString();
                existingPictureEntity = await pictureTableClient.GetEntityIfExistsAsync<PictureEntity>(pictureEntity.PartitionKey, pictureEntity.RowKey);
            }

            BlobClient? cloudBlockBlob = blobContainerClient.GetBlobClient($"{pictureEntity.RowKey}{Path.GetExtension(filename)}");
            bool fileExists = await cloudBlockBlob.ExistsAsync();
            while (fileExists)
            {
                await cloudBlockBlob.DeleteAsync();
                fileExists = await cloudBlockBlob.ExistsAsync();
            }

            await cloudBlockBlob.UploadAsync(stream);

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
                logger.LogInformation("SAS URI for blob is: {0}", sasUri);

                uri = sasUri.AbsoluteUri;
            }
            else
            {
                logger.LogError("BlobClient must be authorized with Shared Key credentials to create a service SAS.");
                //return new ExceptionResult(new Exception("BlobClient must be authorized with Shared Key credentials to create a service SAS."), false);
            }

            BlobClient? smallPictureCloudBlockBlob = blobContainerClient.GetBlobClient($"{pictureEntity.RowKey}_sml.png");
            fileExists = await smallPictureCloudBlockBlob.ExistsAsync();
            while (fileExists)
            {
                await smallPictureCloudBlockBlob.DeleteAsync();
                fileExists = await smallPictureCloudBlockBlob.ExistsAsync();
            }

            // Resize the image to 20x20
            byte[] fileBytes;
            stream.Position = 0;
            using (MemoryStream memoryStream = new())
            {
                await stream.CopyToAsync(memoryStream);
                fileBytes = memoryStream.ToArray();
            }

            using Image image = Image.Load(fileBytes);
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

            // get all pictures from table
            Pageable<PictureEntity> allPicturesQuery = pictureTableClient.Query<PictureEntity>();
            List<PictureEntity> allPictures = allPicturesQuery.AsPages().SelectMany(page => page.Values).ToList();

            if (allPictures.Count > 0)
            {
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

            pictureEntity.PictureUri = uri;
            pictureEntity.PictureSmlUri = smallPicUri;
            await pictureTableClient.AddEntityAsync(pictureEntity);
            uploadResults.Add(new UploadResult
            {
                FileName = filename,
                StoredFileName = cloudBlockBlob.Name,
                ErrorCode = 0,
                Uploaded = true
            });
        }

        HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(uploadResults);
        return response;
    }
}
