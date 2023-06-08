using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;

namespace Api.Data;

public class EloTable
{
    private readonly TableClient client;

    public EloTable(string storageConnectionString)
    {
        this.client = new TableClient(storageConnectionString, Constants.EloTableName);
        this.client.CreateIfNotExists();
    }

    public List<EloEntity> GetAllEloEntities()
    {
        Pageable<EloEntity> queryEloEntities = this.client.Query<EloEntity>();
        List<EloEntity> eloEntities = queryEloEntities.AsPages().SelectMany(page => page.Values).ToList();
        return eloEntities;
    }

    public List<EloEntity> GetEloEntitiesByPartitionKey(string partitionKey)
    {
        Pageable<EloEntity> queryEloEntities = this.client.Query<EloEntity>(pk => pk.PartitionKey == partitionKey);
        List<EloEntity> eloEntities = queryEloEntities.AsPages().SelectMany(page => page.Values).ToList();
        return eloEntities;
    }

    public List<EloEntity> GetEloEntitiesByRowKey(string rowKey)
    {
        Pageable<EloEntity> queryEloEntities = this.client.Query<EloEntity>(pk => pk.RowKey == rowKey);
        List<EloEntity> eloEntities = queryEloEntities.AsPages().SelectMany(page => page.Values).ToList();
        return eloEntities;
    }

    public async Task<EloEntity?> GetEloEntitiesByPartitionAndRowKey(string partitionKey, string rowKey)
    {
        EloEntity? returnEloEntity = null;
        NullableResponse<EloEntity> existingEloEntity = await this.client.GetEntityIfExistsAsync<EloEntity>(partitionKey, rowKey);
        if (existingEloEntity.HasValue)
        {
            returnEloEntity = existingEloEntity.Value;
        }

        return returnEloEntity;
    }

    public async Task AddEloEntityAsync(EloEntity eloEntity)
    {
        await this.client.AddEntityAsync(eloEntity);
    }

    public async Task UpdateEloEntityAsync(EloEntity eloEntity)
    {
        await this.client.UpdateEntityAsync(eloEntity, ETag.All, TableUpdateMode.Replace);
    }

    public async Task DeleteEloEntityAsync(string partitionKey, string rowKey)
    {
        await this.client.DeleteEntityAsync(partitionKey, rowKey, ETag.All);
    }
}
