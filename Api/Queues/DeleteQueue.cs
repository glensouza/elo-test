using System.Threading.Tasks;
using Azure.Storage.Queues;
using Api.Data;

namespace Api.Queues;

public class DeleteQueue
{
    private readonly QueueClient queueClient;

    public DeleteQueue(string storageConnectionString)
    {
        this.queueClient = new QueueClient(storageConnectionString, Constants.DeleteQueueName);
        this.queueClient.CreateIfNotExists();
    }

    public async Task SendMessageAsync(string picId)
    {
        string notification = QueueHelper.PrepareMessageString(picId);
        await this.queueClient.SendMessageAsync(notification);
    }
}
