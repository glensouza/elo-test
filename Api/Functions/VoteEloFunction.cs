using System;
using System.Net;
using System.Threading.Tasks;
using Api.Data;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Api.Functions;

public class VoteEloFunction
{
    private readonly ILogger logger;
    private readonly TableClient pictureTableClient;
    private readonly TableClient eloTableClient;
    private const int KFactor = 32;

    public VoteEloFunction(ILoggerFactory loggerFactory, PictureTable pictureTable, EloTable eloTable)
    {
        logger = loggerFactory.CreateLogger<VoteEloFunction>();
        pictureTableClient = pictureTable.Client;
        eloTableClient = eloTable.Client;
    }

    [Function("VoteElo")]
    public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        string? winner = req.Query["winner"];
        string? loser = req.Query["loser"];
        if (string.IsNullOrEmpty(winner) || string.IsNullOrEmpty(loser))
        {
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        logger.LogInformation("C# HTTP trigger function processed a request. Winner: {0} -- Loser: {1}", winner, loser);

        NullableResponse<PictureEntity> existingWinnerPictureEntity = await pictureTableClient.GetEntityIfExistsAsync<PictureEntity>("Elo", winner);
        if (!existingWinnerPictureEntity.HasValue)
        {
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        PictureEntity winnerPictureEntity = existingWinnerPictureEntity.Value;

        NullableResponse<PictureEntity> existingLoserEloEntity = await pictureTableClient.GetEntityIfExistsAsync<PictureEntity>("Elo", loser);
        if (!existingLoserEloEntity.HasValue)
        {
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        PictureEntity loserPictureEntity = existingLoserEloEntity.Value;

        EloEntity winnerElo;
        NullableResponse<EloEntity> existingWinnerElo = await eloTableClient.GetEntityIfExistsAsync<EloEntity>(winner, loser);
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
            await eloTableClient.AddEntityAsync(new EloEntity { PartitionKey = winner, RowKey = loser, Won = null });
            winnerElo = await eloTableClient.GetEntityAsync<EloEntity>(winner, loser);
        }

        winnerElo.Won = true;

        EloEntity loserElo;
        NullableResponse<EloEntity> existingLoserElo = await eloTableClient.GetEntityIfExistsAsync<EloEntity>(loser, winner);
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
            await eloTableClient.AddEntityAsync(new EloEntity { PartitionKey = loser, RowKey = winner, Won = null });
            loserElo = await eloTableClient.GetEntityAsync<EloEntity>(loser, winner);
        }

        loserElo.Won = false;

        // Calculate the expected scores for each picture
        double winnerExpectedScore = 1 / (1 + Math.Pow(10, (loserPictureEntity.Rating - winnerPictureEntity.Rating) / 400));
        double loserExpectedScore = 1 / (1 + Math.Pow(10, (winnerPictureEntity.Rating - loserPictureEntity.Rating) / 400));

        // Update the ratings for each picture
        double winnerScore = KFactor * (1 - winnerExpectedScore);
        double loserScore = KFactor * (0 - loserExpectedScore);
        winnerPictureEntity.Rating += winnerScore;
        loserPictureEntity.Rating += loserScore;
        winnerElo.Score = winnerScore;
        loserElo.Score = loserScore;

        await pictureTableClient.UpdateEntityAsync(winnerPictureEntity, ETag.All, TableUpdateMode.Replace);
        await pictureTableClient.UpdateEntityAsync(loserPictureEntity, ETag.All, TableUpdateMode.Replace);
        await eloTableClient.UpdateEntityAsync(winnerElo, ETag.All, TableUpdateMode.Replace);
        await eloTableClient.UpdateEntityAsync(loserElo, ETag.All, TableUpdateMode.Replace);

        return req.CreateResponse(HttpStatusCode.OK);
    }
}
