using System;
using System.Net;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Api
{
    public class VoteEloFunction
    {
        private readonly ILogger logger;
        private readonly TableClient tableClient;
        private const int kFactor = 32;

        public VoteEloFunction(ILoggerFactory loggerFactory, TableClient tableClient)
        {
            this.logger = loggerFactory.CreateLogger<VoteEloFunction>();
            this.tableClient = tableClient;
        }

        [Function("VoteElo")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "voteelo/{winner:alpha}/{loser:alpha}")] HttpRequestData req,
            string winner,
            string loser)
        {
            this.logger.LogInformation("C# HTTP trigger function processed a request. Winner: {0} -- Loser: {1}", winner, loser);


            NullableResponse<PictureEntity> existingWinnerEloEntity = await this.tableClient.GetEntityIfExistsAsync<PictureEntity>("Elo", winner);
            if (!existingWinnerEloEntity.HasValue)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            PictureEntity winnerEloEntity = existingWinnerEloEntity.Value;

            NullableResponse<PictureEntity> existingLoserEloEntity = await this.tableClient.GetEntityIfExistsAsync<PictureEntity>("Elo", loser);
            if (!existingLoserEloEntity.HasValue)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            PictureEntity loserEloEntity = existingLoserEloEntity.Value;

            EloEntity winnerElo;
            NullableResponse<EloEntity> existingWinnerElo = await this.tableClient.GetEntityIfExistsAsync<EloEntity>(winner, loser);
            if (existingWinnerElo.HasValue)
            {
                if (existingWinnerElo.Value.Won != null)
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest);
                }

                winnerElo = existingWinnerElo.Value;
            }
            else
            {
                await this.tableClient.AddEntityAsync(new EloEntity { PartitionKey = winner, RowKey = loser, Won = null });
                winnerElo = await this.tableClient.GetEntityAsync<EloEntity>(winner, loser);
            }

            winnerElo.Won = true;

            EloEntity loserElo;
            NullableResponse<EloEntity> existingLoserElo = await this.tableClient.GetEntityIfExistsAsync<EloEntity>(loser, winner);
            if (existingLoserElo.HasValue)
            {
                if (existingLoserElo.Value.Won != null)
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest);
                }

                loserElo = existingLoserElo.Value;
            }
            else
            {
                await this.tableClient.AddEntityAsync(new EloEntity { PartitionKey = loser, RowKey = winner, Won = null });
                loserElo = await this.tableClient.GetEntityAsync<EloEntity>(loser, winner);
            }

            loserElo.Won = false;

            // Calculate the expected scores for each picture
            double winnerExpectedScore = 1 / (1 + Math.Pow(10, (loserEloEntity.Rating - winnerEloEntity.Rating) / 400));
            double loserExpectedScore = 1 / (1 + Math.Pow(10, (winnerEloEntity.Rating - loserEloEntity.Rating) / 400));

            // Update the ratings for each picture
            winnerEloEntity.Rating += kFactor * (1 - winnerExpectedScore);
            loserEloEntity.Rating += kFactor * (0 - loserExpectedScore);

            await this.tableClient.UpdateEntityAsync(winnerEloEntity, ETag.All, TableUpdateMode.Replace);
            await this.tableClient.UpdateEntityAsync(loserEloEntity, ETag.All, TableUpdateMode.Replace);
            await this.tableClient.UpdateEntityAsync(winnerElo, ETag.All, TableUpdateMode.Replace);
            await this.tableClient.UpdateEntityAsync(loserElo, ETag.All, TableUpdateMode.Replace);

            return req.CreateResponse(HttpStatusCode.OK);
        }
    }
}
