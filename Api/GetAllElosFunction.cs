using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using BlazorApp.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Api;

public class GetAllElosFunction
{
    private readonly ILogger logger;
    private readonly TableClient pictureTableClient;

    public GetAllElosFunction(ILoggerFactory loggerFactory, PictureTable pictureTable)
    {
        this.logger = loggerFactory.CreateLogger<GetAllElosFunction>();
        this.pictureTableClient = pictureTable.Client;
    }

    [Function("GetAllElos")]
    public async Task<HttpResponseData> RunAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
    {
        this.logger.LogInformation("C# HTTP trigger function processed a request.");

        Pageable<PictureEntity> queryPictureEntities = this.pictureTableClient.Query<PictureEntity>();
        List<PictureEntity> pictureEntities = queryPictureEntities.AsPages().SelectMany(page => page.Values).ToList();
        if (pictureEntities.Count == 0)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        List<EloModel> elos = pictureEntities.OrderByDescending(s => s.Rating).ThenBy(s => s.Name).Select(pictureEntity => new EloModel
        {
            PicId = pictureEntity.RowKey,
            Name = pictureEntity.Name,
            PictureUri = pictureEntity.PictureUri,
            Rating = double.Round(pictureEntity.Rating),
            LastUpdated = pictureEntity.Timestamp!.Value.DateTime
        }).ToList();

        HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(elos);
        return response;
    }
}
