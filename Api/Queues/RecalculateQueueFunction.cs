using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Api.Data;
using Azure.Storage.Blobs;
using Api.Helpers;

namespace Api.Queues;

public class RecalculateQueueFunction
{
    private readonly ILogger logger;
    private readonly PictureTable pictureTable;
    private readonly EloTable eloTable;
    private readonly BlobContainerClient blobContainerClient;

    public RecalculateQueueFunction(ILoggerFactory loggerFactory, PictureTable pictureTable, EloTable eloTable, BlobContainerClient blobClient)
    {
        this.logger = loggerFactory.CreateLogger<DeleteQueueFunction>();
        this.pictureTable = pictureTable;
        this.eloTable = eloTable;
        this.blobContainerClient = blobClient;
    }

    [Function("RecalculateQueue")]
    public async Task Run([QueueTrigger(Constants.RecalculateQueueName, Connection = "StorageAccount")] string recalculate)
    {
        this.logger.LogInformation("C# Queue trigger function processed");

        List<PictureEntity> allPictures = this.pictureTable.GetAllPictureEntities();
        foreach (PictureEntity pictureEntity in allPictures)
        {
            pictureEntity.Rating = 1200;
        }

        List<EloEntity> allEloEntities = this.eloTable.GetAllEloEntities().Where(s => s.Won != null).OrderBy(s => s.Timestamp).ToList();
        bool needToRunAgain = true;
        while (needToRunAgain)
        {
            needToRunAgain = false;
            // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
            foreach (EloEntity eloEntity in allEloEntities)
            {
                if (!allEloEntities.Any(s => s.PartitionKey == eloEntity.RowKey && s.RowKey == eloEntity.PartitionKey))
                {
                    continue;
                }

                allEloEntities.RemoveAll(s => s.PartitionKey == eloEntity.RowKey && s.RowKey == eloEntity.PartitionKey);
                needToRunAgain = true;
                break;
            }
        }

        foreach (EloEntity eloEntity in allEloEntities)
        {
            PictureEntity winnerPictureEntity = allPictures.First(s => s.RowKey == (eloEntity.Won == true ? eloEntity.PartitionKey : eloEntity.RowKey));
            PictureEntity loserPictureEntity = allPictures.First(s => s.RowKey == (eloEntity.Won == true ? eloEntity.RowKey : eloEntity.PartitionKey));

            (double winnerScore, double loserScore) = EloCalculator.CalculateElo(winnerPictureEntity.Rating, loserPictureEntity.Rating);

            winnerPictureEntity.Rating += winnerScore;
            loserPictureEntity.Rating += loserScore;
        }

        foreach (PictureEntity pictureEntity in allPictures)
        {
            await this.pictureTable.UpdatePictureEntityAsync(pictureEntity);
        }
    }
}
