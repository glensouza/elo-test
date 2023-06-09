using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Api.Data;
using Api.Queues;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Api.Functions;

public class ResetEloVotingFunction
{
    private readonly ILogger logger;
    private readonly ResetQueue resetQueue;

    public ResetEloVotingFunction(ILoggerFactory loggerFactory, ResetQueue resetQueue)
    {
        this.logger = loggerFactory.CreateLogger<ResetEloVotingFunction>();
        this.resetQueue = resetQueue;
    }

    [Function("ResetEloVoting")]
    public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        this.logger.LogInformation("C# HTTP trigger function processed a request.");
        await this.resetQueue.SendMessageAsync();
        return req.CreateResponse(HttpStatusCode.OK);
    }
}
