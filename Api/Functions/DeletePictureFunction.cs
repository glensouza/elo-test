using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Api.Data;
using Api.Queues;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Api.Functions;

public class DeletePictureFunction
{
    private readonly ILogger logger;
    private readonly PictureTable pictureTable;
    private readonly DeleteQueue deleteQueue;

    public DeletePictureFunction(ILoggerFactory loggerFactory, PictureTable pictureTable, DeleteQueue deleteQueue)
    {
        this.logger = loggerFactory.CreateLogger<DeletePictureFunction>();
        this.pictureTable = pictureTable;
        this.deleteQueue = deleteQueue;
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

        PictureEntity? pictureToDelete = this.pictureTable.GetPictureEntityByRowKey(picId);
        if (pictureToDelete == null)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        await this.pictureTable.DeletePictureEntityAsync(pictureToDelete.RowKey);
        await this.deleteQueue.SendMessageAsync(picId);

        return req.CreateResponse(HttpStatusCode.OK);
    }
}
