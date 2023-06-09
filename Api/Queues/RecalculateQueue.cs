using System.Threading.Tasks;
using Azure.Storage.Queues;
using Api.Data;

namespace Api.Queues;

public class RecalculateQueue
{
    private readonly QueueClient queueClient;

    public RecalculateQueue(string storageConnectionString)
    {
        this.queueClient = new QueueClient(storageConnectionString, Constants.RecalculateQueueName);
        this.queueClient.CreateIfNotExists();
    }

    public async Task SendMessageAsync()
    {
        string notification = QueueHelper.PrepareMessageString(Constants.RecalculateQueueName);
        await this.queueClient.SendMessageAsync(notification);
    }
}
