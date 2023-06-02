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

namespace Api
{
    public class GetAllElosFunction
    {
        private readonly ILogger logger;
        private readonly TableClient tableClient;

        public GetAllElosFunction(ILoggerFactory loggerFactory, TableClient tableClient)
        {
            this.logger = loggerFactory.CreateLogger<GetAllElosFunction>();
            this.tableClient = tableClient;
        }

        [Function("GetAllElos")]
        public async Task<HttpResponseData> RunAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            this.logger.LogInformation("C# HTTP trigger function processed a request.");

            Pageable<PictureEntity> queryEloEntities = this.tableClient.Query<PictureEntity>();
            List<PictureEntity> eloEntities = queryEloEntities.AsPages().SelectMany(page => page.Values).ToList();
            if (eloEntities.Count == 0)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            List<EloModel> elos = eloEntities.Select(pictureEntity => new EloModel
            {
                PicId = pictureEntity.RowKey,
                Name = pictureEntity.Name,
                PictureUri = pictureEntity.PictureUri,
                LastUpdated = pictureEntity.Timestamp!.Value.DateTime
            }).ToList();

            HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
            //response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteAsJsonAsync(elos);

            return response;
        }
    }
}
