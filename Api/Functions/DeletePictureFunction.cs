using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Api.Entities;
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
    private readonly TableClient eloTableClient;
    private readonly BlobContainerClient blobContainerClient;

    public DeletePictureFunction(ILoggerFactory loggerFactory, PictureTable pictureTable, EloTable eloTable, BlobContainerClient blobClient)
    {
        logger = loggerFactory.CreateLogger<DeletePictureFunction>();
        pictureTableClient = pictureTable.Client;
        eloTableClient = eloTable.Client;
        blobContainerClient = blobClient;
    }

    [Function("DeletePicture")]
    public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "delete")] HttpRequestData req)
    {
        logger.LogInformation("C# HTTP trigger function processed a request.");

        string? picId = req.Query["picId"];
        if (string.IsNullOrEmpty(picId))
        {
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        Pageable<EloEntity> queryEloEntities = eloTableClient.Query<EloEntity>(s => s.PartitionKey == picId);
        List<EloEntity> eloEntities = queryEloEntities.AsPages().SelectMany(page => page.Values).ToList();
        foreach (EloEntity eloEntity in eloEntities)
        {
            Pageable<PictureEntity> queryPictureToUpdateEntities = pictureTableClient.Query<PictureEntity>(s => s.RowKey == eloEntity.RowKey);
            PictureEntity? pictureToUpdate = queryPictureToUpdateEntities.AsPages().SelectMany(page => page.Values).FirstOrDefault();
            if (pictureToUpdate != null && eloEntity.Score != null)
            {
                pictureToUpdate.Rating += (double)eloEntity.Score;
                await pictureTableClient.UpdateEntityAsync(pictureToUpdate, ETag.All, TableUpdateMode.Replace);
            }

            await eloTableClient.DeleteEntityAsync(picId, eloEntity.RowKey, ETag.All);
            await eloTableClient.DeleteEntityAsync(eloEntity.RowKey, picId, ETag.All);
        }

        queryEloEntities = eloTableClient.Query<EloEntity>(s => s.RowKey == picId);
        eloEntities = queryEloEntities.AsPages().SelectMany(page => page.Values).ToList();
        foreach (EloEntity eloEntity in eloEntities)
        {
            await eloTableClient.DeleteEntityAsync(picId, eloEntity.RowKey, ETag.All);
            await eloTableClient.DeleteEntityAsync(eloEntity.RowKey, picId, ETag.All);
        }

        Pageable<PictureEntity> queryPictureToDeleteEntities = pictureTableClient.Query<PictureEntity>(s => s.RowKey == picId);
        PictureEntity? pictureToDelete = queryPictureToDeleteEntities.AsPages().SelectMany(page => page.Values).FirstOrDefault();
        if (pictureToDelete != null)
        {
            await pictureTableClient.DeleteEntityAsync("Elo", pictureToDelete.RowKey, ETag.All);
        }

        BlobClient? bigPictureCloudBlockBlob = blobContainerClient.GetBlobClient($"{picId}.png");
        bool fileExists = await bigPictureCloudBlockBlob.ExistsAsync();
        while (fileExists)
        {
            await bigPictureCloudBlockBlob.DeleteAsync();
            fileExists = await bigPictureCloudBlockBlob.ExistsAsync();
        }

        BlobClient? smallPictureCloudBlockBlob = blobContainerClient.GetBlobClient($"{picId}_sml.png");
        fileExists = await smallPictureCloudBlockBlob.ExistsAsync();
        while (fileExists)
        {
            await smallPictureCloudBlockBlob.DeleteAsync();
            fileExists = await smallPictureCloudBlockBlob.ExistsAsync();
        }

        return req.CreateResponse(HttpStatusCode.OK);
    }
}
