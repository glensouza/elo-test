using System;
using Azure;
using Azure.Data.Tables;

namespace Api.Data;

public class PictureEntity : ITableEntity
{
    public string PartitionKey { get; set; } = Constants.PictureTablePartitionKey;
    public string RowKey { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string PictureUri { get; set; } = string.Empty;
    public string PictureSmlUri { get; set; } = string.Empty;
    public double Rating { get; set; } = 1200;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}
