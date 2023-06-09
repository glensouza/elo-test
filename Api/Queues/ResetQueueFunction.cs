using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Api.Data;

namespace Api.Queues;

public class ResetQueueFunction
{
    private readonly ILogger logger;
    private readonly PictureTable pictureTable;
    private readonly EloTable eloTable;

    public ResetQueueFunction(ILoggerFactory loggerFactory, PictureTable pictureTable, EloTable eloTable)
    {
        this.logger = loggerFactory.CreateLogger<ResetQueueFunction>();
        this.pictureTable = pictureTable;
        this.eloTable = eloTable;
    }

    [Function("ResetQueue")]
    public async Task Run([QueueTrigger(Constants.ResetQueueName, Connection = "StorageAccount")] string reset)
    {
        this.logger.LogInformation("C# Queue trigger function processed: {0}", reset);

        List<PictureEntity> pictureEntities = this.pictureTable.GetAllPictureEntities();
        foreach (PictureEntity pictureEntity in pictureEntities)
        {
            pictureEntity.Rating = 1200;
            await this.pictureTable.UpdatePictureEntityAsync(pictureEntity);
        }

        List<EloEntity> eloEntities = this.eloTable.GetAllEloEntities();
        foreach (EloEntity eloEntity in eloEntities)
        {
            eloEntity.Won = null;
            await this.eloTable.UpdateEloEntityAsync(eloEntity);
        }
    }
}
