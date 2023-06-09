using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Api.Data;
using System.Linq;

namespace Api.Queues;

public class NewPictureQueueFunction
{
    private readonly ILogger logger;
    private readonly PictureTable pictureTable;
    private readonly EloTable eloTable;

    public NewPictureQueueFunction(ILoggerFactory loggerFactory, PictureTable pictureTable, EloTable eloTable)
    {
        this.logger = loggerFactory.CreateLogger<NewPictureQueueFunction>();
        this.pictureTable = pictureTable;
        this.eloTable = eloTable;
    }

    [Function("NewPictureQueue")]
    public async Task Run([QueueTrigger(Constants.NewPictureQueueName, Connection = "StorageAccount")] string picId)
    {
        this.logger.LogInformation("C# Queue trigger function processed: {0}", picId);

        // get all pictures from table
        List<PictureEntity> allPictures = this.pictureTable.GetAllPictureEntities().Where(s => s.RowKey != picId).ToList();
        if (allPictures.Count == 0)
        {
            return;
        }

        // enter all competitions for this picture
        foreach (string pictureId in allPictures.Select(s => s.RowKey))
        {
            EloEntity? existingEloEntity = await this.eloTable.GetEloEntitiesByPartitionAndRowKey(picId, pictureId);
            if (existingEloEntity != null)
            {
                existingEloEntity.Won = null;
                await this.eloTable.UpdateEloEntityAsync(existingEloEntity);
            }
            else
            {
                await this.eloTable.AddEloEntityAsync(new EloEntity { PartitionKey = picId, RowKey = pictureId, Won = null });
            }

            existingEloEntity = await this.eloTable.GetEloEntitiesByPartitionAndRowKey(pictureId, picId);
            if (existingEloEntity != null)
            {
                existingEloEntity.Won = null;
                await this.eloTable.UpdateEloEntityAsync(existingEloEntity);
            }
            else
            {
                await this.eloTable.AddEloEntityAsync(new EloEntity { PartitionKey = pictureId, RowKey = picId, Won = null });
            }
        }
    }
}
