using System.Linq;
using System.Net;
using Azure.Data.Tables;
using Azure;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Api.Data;

namespace Api.Functions;

public class EditNameFunction
{
    private readonly ILogger logger;
    private readonly TableClient pictureTableClient;

    public EditNameFunction(ILoggerFactory loggerFactory, PictureTable pictureTable)
    {
        this.logger = loggerFactory.CreateLogger<EditNameFunction>();
        this.pictureTableClient = pictureTable.Client;
    }

    [Function("EditName")]
    public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        this.logger.LogInformation("C# HTTP trigger function processed a request.");

        string? picId = req.Query["picId"];
        string? newName = req.Query["name"];
        if (string.IsNullOrEmpty(picId) || string.IsNullOrEmpty(newName))
        {
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        Pageable<PictureEntity> queryPictureToDeleteEntities = this.pictureTableClient.Query<PictureEntity>(s => s.RowKey == picId);
        PictureEntity? pictureToDelete = queryPictureToDeleteEntities.AsPages().SelectMany(page => page.Values).FirstOrDefault();
        if (pictureToDelete != null)
        {
            pictureToDelete.Name = newName;
            await this.pictureTableClient.UpdateEntityAsync(pictureToDelete, ETag.All, TableUpdateMode.Replace);
        }

        HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
        return response;
    }
}
