using System;
using System.Net;
using System.Threading.Tasks;
using Api.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Api.Functions;

public class VoteEloFunction
{
    private readonly ILogger logger;
    private readonly PictureTable pictureTable;
    private readonly EloTable eloTable;
    private const int KFactor = 32;

    public VoteEloFunction(ILoggerFactory loggerFactory, PictureTable pictureTable, EloTable eloTable)
    {
        this.logger = loggerFactory.CreateLogger<VoteEloFunction>();
        this.pictureTable = pictureTable;
        this.eloTable = eloTable;
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

        this.logger.LogInformation("C# HTTP trigger function processed a request. Winner: {0} -- Loser: {1}", winner, loser);

        PictureEntity? winnerPictureEntity = this.pictureTable.GetPictureEntityByRowKey(winner);
        if (winnerPictureEntity is null)
        {
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        PictureEntity? loserPictureEntity = this.pictureTable.GetPictureEntityByRowKey(loser);
        if (loserPictureEntity is null)
        {
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        EloEntity winnerElo;
        EloEntity? existingWinnerElo = await this.eloTable.GetEloEntitiesByPartitionAndRowKey(winner, loser);
        if (existingWinnerElo != null)
        {
            if (existingWinnerElo.Won != null)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
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
                return req.CreateResponse(HttpStatusCode.BadRequest);
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

        await this.pictureTable.UpdatePictureEntityAsync(winnerPictureEntity);
        await this.pictureTable.UpdatePictureEntityAsync(loserPictureEntity);
        await this.eloTable.UpdateEloEntityAsync(winnerElo);
        await this.eloTable.UpdateEloEntityAsync(loserElo);

        return req.CreateResponse(HttpStatusCode.OK);
    }
}
