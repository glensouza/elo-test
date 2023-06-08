using Azure.Data.Tables;

namespace Api.Data;

public class PictureTable
{
    public PictureTable(string storageConnectionString)
    {
        this.Client = new TableClient(storageConnectionString, "picture");
        this.Client.CreateIfNotExists();
    }

    public readonly TableClient Client;
}
