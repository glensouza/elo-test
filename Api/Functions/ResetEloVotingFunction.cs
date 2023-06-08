using System.Collections.Generic;
using System.Linq;
using System.Net;
using Api.Data;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Api.Functions;

public class ResetEloVotingFunction
{
    private readonly ILogger logger;
    private readonly TableClient pictureTableClient;
    private readonly TableClient eloTableClient;

    public ResetEloVotingFunction(ILoggerFactory loggerFactory, PictureTable pictureTable, EloTable eloTable)
    {
        logger = loggerFactory.CreateLogger<ResetEloVotingFunction>();
        pictureTableClient = pictureTable.Client;
        eloTableClient = eloTable.Client;
    }

    [Function("ResetEloVoting")]
    public HttpResponseData Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        logger.LogInformation("C# HTTP trigger function processed a request.");

        Pageable<PictureEntity> queryPictureEntities = pictureTableClient.Query<PictureEntity>();
        List<PictureEntity> pictureEntities = queryPictureEntities.AsPages().SelectMany(page => page.Values).ToList();
        foreach (PictureEntity pictureEntity in pictureEntities)
        {
            pictureEntity.Rating = 1200;
            pictureTableClient.UpdateEntity(pictureEntity, ETag.All, TableUpdateMode.Replace);
        }

        Pageable<EloEntity> queryEloEntities = eloTableClient.Query<EloEntity>();
        List<EloEntity> eloEntities = queryEloEntities.AsPages().SelectMany(page => page.Values).ToList();
        foreach (EloEntity eloEntity in eloEntities)
        {
            eloEntity.Won = null;
            eloTableClient.UpdateEntity(eloEntity, ETag.All, TableUpdateMode.Replace);
        }

        return req.CreateResponse(HttpStatusCode.OK);
    }
}
