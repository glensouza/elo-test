using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Api.Data;
using Api.Helpers;
using Azure.Data.Tables;
using BlazorApp.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Api.Functions;

public class GetElosToVoteFunction
{
    private readonly ILogger logger;
    private readonly EloTable eloTable;
    private readonly PictureTable pictureTable;

    public GetElosToVoteFunction(
        ILoggerFactory loggerFactory,
        EloTable eloTable,
        PictureTable pictureTable)
    {
        this.logger = loggerFactory.CreateLogger<GetElosToVoteFunction>();
        this.eloTable = eloTable;
        this.pictureTable = pictureTable;
    }

    [Function("GetElosToVote")]
    public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
    {
        this.logger.LogInformation("C# HTTP trigger function processed a request.");

        // TODO: Filtering on null doesn't work
        //Pageable<EloEntity> queryEloEntities = this.eloTableClient.Query<EloEntity>(s => s.Won == null); 

        List<EloEntity> eloEntities = this.eloTable.GetAllEloEntities().Where(s => s.Won == null).ToList();

        bool needToRunAgain = true;
        while (needToRunAgain)
        {
            eloEntities.Shuffle();
            needToRunAgain = false;
            // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
            foreach (EloEntity eloEntity in eloEntities)
            {
                if (!eloEntities.Any(s => s.PartitionKey == eloEntity.RowKey && s.RowKey == eloEntity.PartitionKey))
                {
                    continue;
                }

                eloEntities.RemoveAll(s => s.PartitionKey == eloEntity.RowKey && s.RowKey == eloEntity.PartitionKey);
                needToRunAgain = true;
                break;
            }
        }

        List<EloVoteModel> elosToVote = new();
        // ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
        foreach (EloEntity eloEntity in eloEntities)
        {
            PictureEntity? pictureEntity1 = this.pictureTable.GetPictureEntityByRowKey(eloEntity.PartitionKey);
            PictureEntity? pictureEntity2 = this.pictureTable.GetPictureEntityByRowKey(eloEntity.RowKey);

            elosToVote.Add(new EloVoteModel
            {
                PicId1 = pictureEntity1!.RowKey,
                PicId2 = pictureEntity2!.RowKey,
                Name1 = pictureEntity1.Name,
                Name2 = pictureEntity2.Name,
                PictureUri1 = pictureEntity1.PictureUri,
                PictureSmallUri1 = pictureEntity1.PictureSmlUri,
                PictureUri2 = pictureEntity2.PictureUri,
                PictureSmallUri2 = pictureEntity2.PictureSmlUri
            });
        }

        HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(elosToVote);
        return response;
    }
}
