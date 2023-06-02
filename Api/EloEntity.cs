using System;
using Azure;
using Azure.Data.Tables;

namespace Api;

public class EloEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty; // Picture
    public string RowKey { get; set; } = string.Empty; // Competitor
    public bool? Won { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}
