using Azure.Data.Tables;

namespace Api.Entities;

public class PictureTable
{
    public PictureTable(string storageConnectionString)
    {
        Client = new TableClient(storageConnectionString, "picture");
        Client.CreateIfNotExists();
    }

    public readonly TableClient Client;
}
