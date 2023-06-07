using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;
using HttpMultipartParser;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Api;

public class NewEloFunction
{
    private readonly ILogger logger;
    private readonly TableClient pictureTableClient;
    private readonly BlobContainerClient blobContainerClient;

    public NewEloFunction(ILoggerFactory loggerFactory, PictureTable pictureTable, BlobContainerClient blobClient)
    {
        this.logger = loggerFactory.CreateLogger<NewEloFunction>();
        this.pictureTableClient = pictureTable.Client;
        this.blobContainerClient = blobClient;
    }

    [Function("NewElo")]
    public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "put")] HttpRequestData req)
    {
        this.logger.LogInformation("C# HTTP trigger function processed a request.");

        // get form-body
        MultipartFormDataParser parsedFormBody = await MultipartFormDataParser.ParseAsync(req.Body);
        if (parsedFormBody.Files.Count == 0)
        {
            HttpResponseData badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            return badRequestResponse;
        }

        FilePart audioFile = parsedFormBody.Files[0];
        string filename = audioFile.FileName;
        Stream stream = audioFile.Data;

        PictureEntity pictureEntity = new()
        {
            Name = parsedFormBody.HasParameter("name") ? parsedFormBody.GetParameterValues("name").First() : string.Empty
        };

        PictureEntity? existingEloEntity = await this.pictureTableClient.GetEntityAsync<PictureEntity>(pictureEntity.PartitionKey, pictureEntity.RowKey);
        while (existingEloEntity != null)
        {
            pictureEntity.RowKey = Guid.NewGuid().ToString();
            existingEloEntity = await this.pictureTableClient.GetEntityAsync<PictureEntity>(pictureEntity.PartitionKey, pictureEntity.RowKey);
        }
        
        BlobClient? cloudBlockBlob = this.blobContainerClient.GetBlobClient($"{pictureEntity.RowKey}.{Path.GetExtension(filename)}");
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
            this.logger.LogInformation("SAS URI for blob is: {0}", sasUri);

            uri = sasUri.AbsoluteUri;
        }
        else
        {
            this.logger.LogError("BlobClient must be authorized with Shared Key credentials to create a service SAS.");
            //return new ExceptionResult(new Exception("BlobClient must be authorized with Shared Key credentials to create a service SAS."), false);
        }

        pictureEntity.PictureUri = uri;
        await this.pictureTableClient.AddEntityAsync(pictureEntity);

        HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
        return response;
    }
}
