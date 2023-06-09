using System.Threading.Tasks;
using Azure.Storage.Queues;
using Api.Data;

namespace Api.Queues;

public class VoteQueue
{
    private readonly QueueClient queueClient;

    public VoteQueue(string storageConnectionString)
    {
        this.queueClient = new QueueClient(storageConnectionString, Constants.VoteQueueName);
        this.queueClient.CreateIfNotExists();
    }

    public async Task SendMessageAsync(string winnerId, string loserId)
    {
        string notification = QueueHelper.PrepareMessageString($"{winnerId}|{loserId}");
        await this.queueClient.SendMessageAsync(notification);
    }
}
