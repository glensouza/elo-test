using System.Collections.Generic;
using System.Linq;
using System.Net;
using Api.Data;
using Api.Helpers;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Api.Functions
{
    public class GetRandomCarNameFunction
    {
        private readonly ILogger logger;
        private readonly CarNameGenerator carNameGenerator;
        private readonly PictureTable pictureTable;

        public GetRandomCarNameFunction(ILoggerFactory loggerFactory, CarNameGenerator carNameGenerator, PictureTable pictureTable)
        {
            this.logger = loggerFactory.CreateLogger<GetRandomCarNameFunction>();
            this.carNameGenerator = carNameGenerator;
            this.pictureTable = pictureTable;
        }

        [Function("GetRandomCarName")]
        public HttpResponseData Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            this.logger.LogInformation("C# HTTP trigger function processed a request.");

            string carName;
            List<PictureEntity> queryUniquePictureEntities;
            do
            {
                // verify uniqueness of name
                carName = this.carNameGenerator.GetRandomCarName();
                string name = carName;
                queryUniquePictureEntities = this.pictureTable.GetPictureEntitiesByName(name);
            } while (queryUniquePictureEntities.Any());

            HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
            response.WriteString(carName);
            return response;
        }
    }
}
