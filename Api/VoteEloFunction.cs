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
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "voteelo/{winner:alpha}/{loser:alpha}")] HttpRequestData req,
            string winner, string loser)
        {
            this.logger.LogInformation($"C# HTTP trigger function processed a request. Winner: {winner} -- Loser: {loser}");

            PictureEntity? winnerEloEntity = null;
            PictureEntity? loserEloEntity = null;

            try
            {
                winnerEloEntity = await this.tableClient.GetEntityAsync<PictureEntity>("Elo", winner);
                loserEloEntity = await this.tableClient.GetEntityAsync<PictureEntity>("Elo", loser);
            }
            catch (Exception)
            {
                // at least one entity does not exist
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            NullableResponse<EloEntity> winnerElo = await this.tableClient.GetEntityIfExistsAsync<EloEntity>(winner, loser);
            NullableResponse<EloEntity> loserElo = await this.tableClient.GetEntityIfExistsAsync<EloEntity>(loser, winner);

            if (winnerElo.HasValue || loserElo.HasValue)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            await this.tableClient.AddEntityAsync(new EloEntity { PartitionKey = winner, RowKey = loser });
            await this.tableClient.AddEntityAsync(new EloEntity { PartitionKey = loser, RowKey = winner });

            // Calculate the expected scores for each picture
            double winnerExpectedScore = 1 / (1 + Math.Pow(10, (loserEloEntity.Rating - winnerEloEntity.Rating) / 400));
            double loserExpectedScore = 1 / (1 + Math.Pow(10, (winnerEloEntity.Rating - loserEloEntity.Rating) / 400));

            // Update the ratings for each picture
            winnerEloEntity.Rating += kFactor * (1 - winnerExpectedScore);
            loserEloEntity.Rating += kFactor * (0 - loserExpectedScore);

            await this.tableClient.UpdateEntityAsync(winnerEloEntity, ETag.All, TableUpdateMode.Replace);
            await this.tableClient.UpdateEntityAsync(loserEloEntity, ETag.All, TableUpdateMode.Replace);

            return req.CreateResponse(HttpStatusCode.OK);
        }
    }
}
