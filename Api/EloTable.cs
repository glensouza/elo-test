using Azure.Data.Tables;

namespace Api;

public class PictureTable
{
    public PictureTable(string storageConnectionString)
    {
        this.Client = new TableClient(storageConnectionString, "picture");
        this.Client.CreateIfNotExists();
    }

    public readonly TableClient Client;
}
