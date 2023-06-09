namespace Api.Data;

public static class Constants
{
    public const string PictureTablePartitionKey = "Picture";
    public const string PictureTableName = "pictures";
    public const string EloTableName = "votes";
    public const string DeleteQueueName = "delete";
    public const string ResetQueueName = "reset";
    public const string VoteQueueName = "vote";
    public const string NewPictureQueueName = "newpic";
    public const string RecalculateQueueName = "recalculate";
}
