using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Api;

public class DeletePictureFunction
{
    private readonly ILogger logger;
    private readonly TableClient pictureTableClient;
    private readonly TableClient eloTableClient;

    public DeletePictureFunction(ILoggerFactory loggerFactory, PictureTable pictureTable, EloTable eloTable)
    {
        this.logger = loggerFactory.CreateLogger<DeletePictureFunction>();
        this.pictureTableClient = pictureTable.Client;
        this.eloTableClient = eloTable.Client;
    }

    [Function("DeletePicture")]
    public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "delete")] HttpRequestData req)
    {
        this.logger.LogInformation("C# HTTP trigger function processed a request.");

        string? picId = req.Query["picId"];
        if (string.IsNullOrEmpty(picId))
        {
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        Pageable<EloEntity> queryEloEntities = this.eloTableClient.Query<EloEntity>(s => s.PartitionKey == picId);
        List<EloEntity> eloEntities = queryEloEntities.AsPages().SelectMany(page => page.Values).ToList();
        foreach (EloEntity eloEntity in eloEntities)
        {
            Pageable<PictureEntity> queryPictureToUpdateEntities = this.pictureTableClient.Query<PictureEntity>(s => s.PartitionKey == picId);
            PictureEntity? pictureToUpdate = queryPictureToUpdateEntities.AsPages().SelectMany(page => page.Values).FirstOrDefault();
            if (pictureToUpdate != null)
            {
                // TODO: figure out if add or subtract
                pictureToUpdate.Rating += (double)eloEntity.Score!;
                await this.pictureTableClient.UpdateEntityAsync(pictureToUpdate, ETag.All, TableUpdateMode.Replace);
            }

            await this.eloTableClient.DeleteEntityAsync(picId, eloEntity.RowKey, ETag.All);
            await this.eloTableClient.DeleteEntityAsync(eloEntity.RowKey, picId, ETag.All);
        }

        Pageable<PictureEntity> queryPictureToDeleteEntities = this.pictureTableClient.Query<PictureEntity>(s => s.PartitionKey == picId);
        PictureEntity? pictureToDelete = queryPictureToDeleteEntities.AsPages().SelectMany(page => page.Values).FirstOrDefault();
        if (pictureToDelete != null)
        {
            await this.pictureTableClient.DeleteEntityAsync("Elo", pictureToDelete.RowKey, ETag.All);
        }

        return req.CreateResponse(HttpStatusCode.OK);
    }
}
