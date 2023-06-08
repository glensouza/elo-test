using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Api.Data;

namespace Api.Functions;

public class EditNameFunction
{
    private readonly ILogger logger;
    private readonly PictureTable pictureTable;

    public EditNameFunction(ILoggerFactory loggerFactory, PictureTable pictureTable)
    {
        this.logger = loggerFactory.CreateLogger<EditNameFunction>();
        this.pictureTable = pictureTable;
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

        PictureEntity? pictureToDelete = this.pictureTable.GetPictureEntityByRowKey(picId);
        if (pictureToDelete != null)
        {
            pictureToDelete.Name = newName;
            await this.pictureTable.UpdatePictureEntityAsync(pictureToDelete);
        }

        HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
        return response;
    }
}
