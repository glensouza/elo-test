using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
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
    private readonly PictureTable pictureTable;
    private readonly EloTable eloTable;

    public ResetEloVotingFunction(ILoggerFactory loggerFactory, PictureTable pictureTable, EloTable eloTable)
    {
        this.logger = loggerFactory.CreateLogger<ResetEloVotingFunction>();
        this.pictureTable = pictureTable;
        this.eloTable = eloTable;
    }

    [Function("ResetEloVoting")]
    public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        this.logger.LogInformation("C# HTTP trigger function processed a request.");

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

        return req.CreateResponse(HttpStatusCode.OK);
    }
}
