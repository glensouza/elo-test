using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Api.Data;
using Azure.Storage.Blobs;

namespace Api.Queues;

public class DeleteQueueFunction
{
    private readonly ILogger logger;
    private readonly PictureTable pictureTable;
    private readonly EloTable eloTable;
    private readonly BlobContainerClient blobContainerClient;

    public DeleteQueueFunction(ILoggerFactory loggerFactory, PictureTable pictureTable, EloTable eloTable, BlobContainerClient blobClient)
    {
        this.logger = loggerFactory.CreateLogger<DeleteQueueFunction>();
        this.pictureTable = pictureTable;
        this.eloTable = eloTable;
        this.blobContainerClient = blobClient;
    }

    [Function("DeleteQueue")]
    public async Task Run([QueueTrigger(Constants.DeleteQueueName, Connection = "StorageAccount")] string picId)
    {
        this.logger.LogInformation("C# Queue trigger function processed: {0}", picId);

        List<EloEntity> eloEntities = this.eloTable.GetEloEntitiesByPartitionKey(picId);
        foreach (EloEntity eloEntity in eloEntities)
        {
            PictureEntity? pictureToUpdate = this.pictureTable.GetPictureEntityByRowKey(eloEntity.RowKey);
            if (pictureToUpdate != null && eloEntity.Score != null)
            {
                pictureToUpdate.Rating += (double)eloEntity.Score;
                await this.pictureTable.UpdatePictureEntityAsync(pictureToUpdate);
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
    }
}
