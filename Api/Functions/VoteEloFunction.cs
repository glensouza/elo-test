using System.Net;
using System.Threading.Tasks;
using Api.Queues;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Api.Functions;

public class VoteEloFunction
{
    private readonly ILogger logger;
    private readonly VoteQueue voteQueue;

    public VoteEloFunction(ILoggerFactory loggerFactory, VoteQueue voteQueue)
    {
        this.logger = loggerFactory.CreateLogger<VoteEloFunction>();
        this.voteQueue = voteQueue;
    }

    [Function("VoteElo")]
    public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        string? winner = req.Query["winner"];
        string? loser = req.Query["loser"];
        if (string.IsNullOrEmpty(winner) || string.IsNullOrEmpty(loser))
        {
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        this.logger.LogInformation("C# HTTP trigger function processed a request. Winner: {0} -- Loser: {1}", winner, loser);

        await this.voteQueue.SendMessageAsync(winner, loser);

        return req.CreateResponse(HttpStatusCode.OK);
    }
}
