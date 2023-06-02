using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Api
{
    public class GetElosToVoteFunction
    {
        private readonly ILogger logger;

        public GetElosToVoteFunction(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger<GetElosToVoteFunction>();
        }

        [Function("GetElosToVote")]
        public HttpResponseData Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
        {
            this.logger.LogInformation("C# HTTP trigger function processed a request.");

            HttpResponseData? response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            response.WriteString("Welcome to Azure Functions!");

            return response;
        }
    }
}
