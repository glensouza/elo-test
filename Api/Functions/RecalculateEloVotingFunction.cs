using System.Net;
using System.Threading.Tasks;
using Api.Queues;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Api.Functions
{
    public class RecalculateEloVotingFunction
    {
        private readonly ILogger logger;
        private readonly RecalculateQueue recalculateQueue;

        public RecalculateEloVotingFunction(ILoggerFactory loggerFactory, RecalculateQueue recalculateQueue)
        {
            this.logger = loggerFactory.CreateLogger<RecalculateEloVotingFunction>();
            this.recalculateQueue = recalculateQueue;
        }

        [Function("RecalculateEloVoting")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            this.logger.LogInformation("C# HTTP trigger function processed a request.");
            await this.recalculateQueue.SendMessageAsync();
            return req.CreateResponse(HttpStatusCode.OK); ;
        }
    }
}
