using System.Collections.Generic;
using System.Linq;
using System.Net;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Api;

public class ResetEloVotingFunction
{
    private readonly ILogger logger;
    private readonly TableClient pictureTableClient;
    private readonly TableClient eloTableClient;

    public ResetEloVotingFunction(ILoggerFactory loggerFactory, PictureTable pictureTable, EloTable eloTable)
    {
        this.logger = loggerFactory.CreateLogger<ResetEloVotingFunction>();
        this.pictureTableClient = pictureTable.Client;
        this.eloTableClient = eloTable.Client;
    }

    [Function("ResetEloVoting")]
    public HttpResponseData Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        this.logger.LogInformation("C# HTTP trigger function processed a request.");

        Pageable<PictureEntity> queryPictureEntities = this.pictureTableClient.Query<PictureEntity>();
        List<PictureEntity> pictureEntities = queryPictureEntities.AsPages().SelectMany(page => page.Values).ToList();
        foreach (PictureEntity pictureEntity in pictureEntities)
        {
            pictureEntity.Rating = 1200;
            this.pictureTableClient.UpdateEntity(pictureEntity, ETag.All, TableUpdateMode.Replace);
        }

        Pageable<EloEntity> queryEloEntities = this.eloTableClient.Query<EloEntity>();
        List<EloEntity> eloEntities = queryEloEntities.AsPages().SelectMany(page => page.Values).ToList();
        foreach (EloEntity eloEntity in eloEntities)
        {
            eloEntity.Won = null;
            this.eloTableClient.UpdateEntity(eloEntity, ETag.All, TableUpdateMode.Replace);
        }

        return req.CreateResponse(HttpStatusCode.OK);
    }
}
