﻿using Azure.Data.Tables;

namespace Api;

public class EloTable
{
    public EloTable(string storageConnectionString)
    {
        this.Client = new TableClient(storageConnectionString, "elo");
        this.Client.CreateIfNotExists();
    }

    public readonly TableClient Client;
}
