using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Api;

public class DeletePictureFunction
{
    private readonly ILogger logger;
    private readonly TableClient pictureTableClient;
    private readonly TableClient eloTableClient;
    private readonly BlobContainerClient blobContainerClient;

    public DeletePictureFunction(ILoggerFactory loggerFactory, PictureTable pictureTable, EloTable eloTable, BlobContainerClient blobClient)
    {
        this.logger = loggerFactory.CreateLogger<DeletePictureFunction>();
        this.pictureTableClient = pictureTable.Client;
        this.eloTableClient = eloTable.Client;
        this.blobContainerClient = blobClient;
    }

    [Function("DeletePicture")]
    public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "delete")] HttpRequestData req)
    {
        this.logger.LogInformation("C# HTTP trigger function processed a request.");

        string? picId = req.Query["picId"];
        if (string.IsNullOrEmpty(picId))
        {
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        Pageable<EloEntity> queryEloEntities = this.eloTableClient.Query<EloEntity>(s => s.PartitionKey == picId);
        List<EloEntity> eloEntities = queryEloEntities.AsPages().SelectMany(page => page.Values).ToList();
        foreach (EloEntity eloEntity in eloEntities)
        {
            Pageable<PictureEntity> queryPictureToUpdateEntities = this.pictureTableClient.Query<PictureEntity>(s => s.RowKey == eloEntity.RowKey);
            PictureEntity? pictureToUpdate = queryPictureToUpdateEntities.AsPages().SelectMany(page => page.Values).FirstOrDefault();
            if (pictureToUpdate != null)
            {
                pictureToUpdate.Rating += (double)eloEntity.Score!;
                await this.pictureTableClient.UpdateEntityAsync(pictureToUpdate, ETag.All, TableUpdateMode.Replace);
            }

            await this.eloTableClient.DeleteEntityAsync(picId, eloEntity.RowKey, ETag.All);
            await this.eloTableClient.DeleteEntityAsync(eloEntity.RowKey, picId, ETag.All);
        }

        queryEloEntities = this.eloTableClient.Query<EloEntity>(s => s.RowKey == picId);
        eloEntities = queryEloEntities.AsPages().SelectMany(page => page.Values).ToList();
        foreach (EloEntity eloEntity in eloEntities)
        {
            await this.eloTableClient.DeleteEntityAsync(picId, eloEntity.RowKey, ETag.All);
            await this.eloTableClient.DeleteEntityAsync(eloEntity.RowKey, picId, ETag.All);
        }

        Pageable<PictureEntity> queryPictureToDeleteEntities = this.pictureTableClient.Query<PictureEntity>(s => s.RowKey == picId);
        PictureEntity? pictureToDelete = queryPictureToDeleteEntities.AsPages().SelectMany(page => page.Values).FirstOrDefault();
        if (pictureToDelete != null)
        {
            await this.pictureTableClient.DeleteEntityAsync("Elo", pictureToDelete.RowKey, ETag.All);
        }

        BlobClient? bigPictureCloudBlockBlob = this.blobContainerClient.GetBlobClient($"{picId}.png");
        bool fileExists = await bigPictureCloudBlockBlob.ExistsAsync();
        while (fileExists)
        {
            await bigPictureCloudBlockBlob.DeleteAsync();
            fileExists = await bigPictureCloudBlockBlob.ExistsAsync();
        }

        BlobClient? smallPictureCloudBlockBlob = this.blobContainerClient.GetBlobClient($"{picId}_sml.png");
        fileExists = await smallPictureCloudBlockBlob.ExistsAsync();
        while (fileExists)
        {
            await smallPictureCloudBlockBlob.DeleteAsync();
            fileExists = await smallPictureCloudBlockBlob.ExistsAsync();
        }

        return req.CreateResponse(HttpStatusCode.OK);
    }
}
