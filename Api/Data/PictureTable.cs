using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;

namespace Api.Data;

public class PictureTable
{
    private readonly TableClient client;

    public PictureTable(string storageConnectionString)
    {
        this.client = new TableClient(storageConnectionString, Constants.PictureTableName);
        this.client.CreateIfNotExists();
    }

    public List<PictureEntity> GetAllPictureEntities()
    {
        Pageable<PictureEntity> queryPictureTables = this.client.Query<PictureEntity>();
        List<PictureEntity> pictureTables = queryPictureTables.AsPages().SelectMany(page => page.Values).ToList();
        return pictureTables;
    }

    public PictureEntity? GetPictureEntityByRowKey(string rowKey)
    {
        Pageable<PictureEntity> queryPictureToUpdateEntities = this.client.Query<PictureEntity>(s => s.RowKey == rowKey);
        PictureEntity? pictureToUpdate = queryPictureToUpdateEntities.AsPages().SelectMany(page => page.Values).FirstOrDefault();
        return pictureToUpdate;
    }

    public List<PictureEntity> GetPictureEntitiesByName(string name)
    {
        List<PictureEntity> returnPictureEntities = new();
        Pageable<PictureEntity> queryPictureEntities = this.client.Query<PictureEntity>(s => s.Name == name);
        PictureEntity? existingPictureEntity = queryPictureEntities.AsPages().SelectMany(page => page.Values).FirstOrDefault();
        if (existingPictureEntity != null)
        {
            returnPictureEntities.Add(existingPictureEntity);
        }

        return returnPictureEntities;
    }

    public async Task AddPictureEntityAsync(PictureEntity pictureEntity)
    {
        await this.client.AddEntityAsync(pictureEntity);
    }

    public async Task UpdatePictureEntityAsync(PictureEntity pictureEntity)
    {
        await this.client.UpdateEntityAsync(pictureEntity, ETag.All, TableUpdateMode.Replace);
    }

    public async Task DeletePictureEntityAsync(string rowKey)
    {
        await this.client.DeleteEntityAsync(Constants.PictureTablePartitionKey, rowKey);
    }
}
