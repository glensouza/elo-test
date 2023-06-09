using System.Threading.Tasks;
using Api.Data;
using Api.Helpers;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Api.Queues
{
    public class VoteQueueFunction
    {
        private readonly ILogger logger;
        private readonly PictureTable pictureTable;
        private readonly EloTable eloTable;

        public VoteQueueFunction(ILoggerFactory loggerFactory, PictureTable pictureTable, EloTable eloTable)
        {
            this.logger = loggerFactory.CreateLogger<VoteQueueFunction>();
            this.pictureTable = pictureTable;
            this.eloTable = eloTable;
        }

        [Function("VoteQueue")]
        public async Task Run([QueueTrigger(Constants.VoteQueueName, Connection = "StorageAccount")] string voteResult)
        {
            this.logger.LogInformation("C# Queue trigger function processed: {0}", voteResult);

            string[] voteResultParts = voteResult.Split('|');
            string winner = voteResultParts[0];
            string loser = voteResultParts[1];

            PictureEntity? winnerPictureEntity = this.pictureTable.GetPictureEntityByRowKey(winner);
            if (winnerPictureEntity is null)
            {
                return;
            }

            PictureEntity? loserPictureEntity = this.pictureTable.GetPictureEntityByRowKey(loser);
            if (loserPictureEntity is null)
            {
                return;
            }

            EloEntity winnerElo;
            EloEntity? existingWinnerElo = await this.eloTable.GetEloEntitiesByPartitionAndRowKey(winner, loser);
            if (existingWinnerElo != null)
            {
                if (existingWinnerElo.Won != null)
                {
                    return;
                }

                winnerElo = existingWinnerElo;
            }
            else
            {
                await this.eloTable.AddEloEntityAsync(new EloEntity { PartitionKey = winner, RowKey = loser, Won = null });
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                winnerElo = await this.eloTable.GetEloEntitiesByPartitionAndRowKey(winner, loser);
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
            }

            winnerElo!.Won = true;

            EloEntity loserElo;
            EloEntity? existingLoserElo = await this.eloTable.GetEloEntitiesByPartitionAndRowKey(loser, winner);
            if (existingLoserElo != null)
            {
                if (existingLoserElo.Won != null)
                {
                    return;
                }

                loserElo = existingLoserElo;
            }
            else
            {
                await this.eloTable.AddEloEntityAsync(new EloEntity { PartitionKey = loser, RowKey = winner, Won = null });
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                loserElo = await this.eloTable.GetEloEntitiesByPartitionAndRowKey(loser, winner);
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
            }

            loserElo!.Won = false;

            (double winnerScore, double loserScore) = EloCalculator.CalculateElo(winnerPictureEntity.Rating, loserPictureEntity.Rating);

            winnerPictureEntity.Rating += winnerScore;
            loserPictureEntity.Rating += loserScore;
            winnerElo.Score = winnerScore;
            loserElo.Score = loserScore;

            await this.pictureTable.UpdatePictureEntityAsync(winnerPictureEntity);
            await this.pictureTable.UpdatePictureEntityAsync(loserPictureEntity);
            await this.eloTable.UpdateEloEntityAsync(winnerElo);
            await this.eloTable.UpdateEloEntityAsync(loserElo);
        }
    }
}
