using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Api.Data;
using Azure;
using Azure.Data.Tables;
using BlazorApp.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Api.Functions;

public class GetAllElosFunction
{
    private readonly ILogger logger;
    private readonly PictureTable pictureTableClient;

    public GetAllElosFunction(ILoggerFactory loggerFactory, PictureTable pictureTable)
    {
        this.logger = loggerFactory.CreateLogger<GetAllElosFunction>();
        this.pictureTableClient = pictureTable;
    }

    [Function("GetAllElos")]
    public async Task<HttpResponseData> RunAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
    {
        this.logger.LogInformation("C# HTTP trigger function processed a request.");

        List<PictureEntity> pictureEntities = this.pictureTableClient.GetAllPictureEntities();
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
