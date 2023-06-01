using System.Net.Http.Json;
using BlazorApp.Shared;
using Microsoft.JSInterop;

namespace BlazorApp.Client.Data;

public class LocalStore
{
    private readonly HttpClient httpClient;
    private readonly IJSRuntime js;
    private const string LocalEdits = "localedits";
    private const string ServerData = "serverdata";
    private const string ApiEloDetailsUrl = "api/elo/details";
    private const string ApiNewElosUrl = "api/elo/changedelos";
    private const string JsGetAll = "localStore.getAll";
    private const string JsGetFirstFromIndex = "localStore.getFirstFromIndex";
    private const string JsPutAllFromJson = "localStore.putAllFromJson";
    private const string JsPut = "localStore.put";
    private const string JsDelete = "localStore.delete";
    private const string JsGet = "localStore.get";

    public LocalStore(HttpClient httpClient, IJSRuntime js)
    {
        this.httpClient = httpClient;
        this.js = js;
    }

    public ValueTask<EloModel[]> GetOutstandingLocalEditsAsync()
    {
        return this.js.InvokeAsync<EloModel[]>(JsGetAll, LocalEdits);
    }

    public async Task SynchronizeAsync()
    {
        // If there are local edits, always send them first
        foreach (EloModel editedElo in await this.GetOutstandingLocalEditsAsync())
        {
            HttpResponseMessage response = await this.httpClient.PutAsJsonAsync(ApiEloDetailsUrl, editedElo);
            response.EnsureSuccessStatusCode();
            await this.DeleteAsync(LocalEdits, editedElo.PicId);
        }

        await this.FetchChangesAsync();
    }

    // If there's an outstanding local edit, use that. If not, use the server data.
    public async Task<EloModel?> GetElo(string picId)
    {
        return await this.GetAsync<EloModel>(LocalEdits, picId)
               ?? await this.GetAsync<EloModel>(ServerData, picId);
    }

    public ValueTask SaveAsync(EloModel elo) => this.PutAsync(LocalEdits, null, elo);

    private async Task FetchChangesAsync()
    {
        EloModel? mostRecentlyUpdated = await this.js.InvokeAsync<EloModel>(JsGetFirstFromIndex, ServerData);
        DateTime since = mostRecentlyUpdated?.LastUpdated ?? DateTime.MinValue;
        string json = await this.httpClient.GetStringAsync(ApiNewElosUrl);
        await this.js.InvokeVoidAsync(JsPutAllFromJson, ServerData, json);
    }

    private ValueTask<T> GetAsync<T>(string storeName, object key) => this.js.InvokeAsync<T>(JsGet, storeName, key);

    private ValueTask PutAsync<T>(string storeName, object? key, T value) => this.js.InvokeVoidAsync(JsPut, storeName, key, value);

    private ValueTask DeleteAsync(string storeName, object key) => this.js.InvokeVoidAsync(JsDelete, storeName, key);
}
