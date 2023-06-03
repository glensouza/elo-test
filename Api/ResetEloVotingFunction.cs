using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Api
{
    public class ResetEloVotingFunction
    {
        private readonly ILogger _logger;

        public ResetEloVotingFunction(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ResetEloVotingFunction>();
        }

        [Function("ResetEloVoting")]
        public HttpResponseData Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            response.WriteString("Welcome to Azure Functions!");

            return response;
        }
    }
}
