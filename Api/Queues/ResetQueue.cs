using System.Threading.Tasks;
using Azure.Storage.Queues;
using Api.Data;

namespace Api.Queues;

public class ResetQueue
{
    private readonly QueueClient queueClient;

    public ResetQueue(string storageConnectionString)
    {
        this.queueClient = new QueueClient(storageConnectionString, Constants.ResetQueueName);
        this.queueClient.CreateIfNotExists();
    }

    public async Task SendMessageAsync()
    {
        string notification = QueueHelper.PrepareMessageString("Reset");
        await this.queueClient.SendMessageAsync(notification);
    }
}
