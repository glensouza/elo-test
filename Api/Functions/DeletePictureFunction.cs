using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Api.Data;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Api.Functions;

public class DeletePictureFunction
{
    private readonly ILogger logger;
    private readonly TableClient pictureTableClient;
    private readonly EloTable eloTable;
    private readonly BlobContainerClient blobContainerClient;

    public DeletePictureFunction(ILoggerFactory loggerFactory, PictureTable pictureTable, EloTable eloTable, BlobContainerClient blobClient)
    {
        this.logger = loggerFactory.CreateLogger<DeletePictureFunction>();
        this.pictureTableClient = pictureTable.Client;
        this.eloTable = eloTable;
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

        List<EloEntity> eloEntities = this.eloTable.GetEloEntitiesByPartitionKey(picId);
        foreach (EloEntity eloEntity in eloEntities)
        {
            Pageable<PictureEntity> queryPictureToUpdateEntities = this.pictureTableClient.Query<PictureEntity>(s => s.RowKey == eloEntity.RowKey);
            PictureEntity? pictureToUpdate = queryPictureToUpdateEntities.AsPages().SelectMany(page => page.Values).FirstOrDefault();
            if (pictureToUpdate != null && eloEntity.Score != null)
            {
                pictureToUpdate.Rating += (double)eloEntity.Score;
                await this.pictureTableClient.UpdateEntityAsync(pictureToUpdate, ETag.All, TableUpdateMode.Replace);
            }

            await this.eloTable.DeleteEloEntityAsync(picId, eloEntity.RowKey);
            await this.eloTable.DeleteEloEntityAsync(eloEntity.RowKey, picId);
        }

        eloEntities = this.eloTable.GetEloEntitiesByRowKey(picId);
        foreach (EloEntity eloEntity in eloEntities)
        {
            await this.eloTable.DeleteEloEntityAsync(picId, eloEntity.RowKey);
            await this.eloTable.DeleteEloEntityAsync(eloEntity.RowKey, picId);
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
