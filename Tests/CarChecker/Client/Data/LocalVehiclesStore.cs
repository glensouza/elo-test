using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Threading.Tasks;
using CarChecker.Shared;
using Microsoft.JSInterop;

namespace CarChecker.Client.Data
{
    // To support offline use, we use this simple local data repository
    // instead of performing data access directly against the server.
    // This would not be needed if we assumed that network access was always
    // available.

    public class LocalVehiclesStore
    {
        private readonly HttpClient httpClient;
        private readonly IJSRuntime js;

        public LocalVehiclesStore(HttpClient httpClient, IJSRuntime js)
        {
            this.httpClient = httpClient;
            this.js = js;
        }

        public ValueTask<Vehicle[]> GetOutstandingLocalEditsAsync()
        {
            return this.js.InvokeAsync<Vehicle[]>(
                "localVehicleStore.getAll", "localedits");
        }

        public async Task SynchronizeAsync()
        {
            // If there are local edits, always send them first
            foreach (Vehicle editedVehicle in await this.GetOutstandingLocalEditsAsync())
            {
                (await this.httpClient.PutAsJsonAsync("api/vehicle/details", editedVehicle)).EnsureSuccessStatusCode();
                await this.DeleteAsync("localedits", editedVehicle.LicenseNumber);
            }

            await this.FetchChangesAsync();
        }

        public ValueTask SaveUserAccountAsync(ClaimsPrincipal user)
        {
            return user != null
                ? this.PutAsync("metadata", "userAccount", user.Claims.Select(c => new ClaimData { Type = c.Type, Value = c.Value }))
                : this.DeleteAsync("metadata", "userAccount");
        }

        public async Task<ClaimsPrincipal> LoadUserAccountAsync()
        {
            ClaimData[] storedClaims = await this.GetAsync<ClaimData[]>("metadata", "userAccount");
            return storedClaims != null
                ? new ClaimsPrincipal(new ClaimsIdentity(storedClaims.Select(c => new Claim(c.Type, c.Value)), "appAuth"))
                : new ClaimsPrincipal(new ClaimsIdentity());
        }

        public ValueTask<string[]> Autocomplete(string prefix)
            =>
                this.js.InvokeAsync<string[]>("localVehicleStore.autocompleteKeys", "serverdata", prefix, 5);

        // If there's an outstanding local edit, use that. If not, use the server data.
        public async Task<Vehicle> GetVehicle(string licenseNumber)
            => await this.GetAsync<Vehicle>("localedits", licenseNumber)
            ?? await this.GetAsync<Vehicle>("serverdata", licenseNumber);

        public async ValueTask<DateTime?> GetLastUpdateDateAsync()
        {
            string value = await this.GetAsync<string>("metadata", "lastUpdateDate");
            return value == null ? (DateTime?)null : DateTime.Parse(value);
        }

        public ValueTask SaveVehicleAsync(Vehicle vehicle)
            =>
                this.PutAsync("localedits", null, vehicle);

        private async Task FetchChangesAsync()
        {
            Vehicle mostRecentlyUpdated = await this.js.InvokeAsync<Vehicle>("localVehicleStore.getFirstFromIndex", "serverdata", "lastUpdated", "prev");
            DateTime since = mostRecentlyUpdated?.LastUpdated ?? DateTime.MinValue;
            string json = await this.httpClient.GetStringAsync($"api/vehicle/changedvehicles?since={since:o}");
            await this.js.InvokeVoidAsync("localVehicleStore.putAllFromJson", "serverdata", json);
            await this.PutAsync("metadata", "lastUpdateDate", DateTime.Now.ToString("o"));
        }

        private ValueTask<T> GetAsync<T>(string storeName, object key)
            =>
                this.js.InvokeAsync<T>("localVehicleStore.get", storeName, key);

        private ValueTask PutAsync<T>(string storeName, object key, T value)
            =>
                this.js.InvokeVoidAsync("localVehicleStore.put", storeName, key, value);

        private ValueTask DeleteAsync(string storeName, object key)
            =>
                this.js.InvokeVoidAsync("localVehicleStore.delete", storeName, key);

        private class ClaimData
        {
            public string Type { get; set; }
            public string Value { get; set; }
        }
    }
}
