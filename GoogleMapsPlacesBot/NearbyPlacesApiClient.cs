using Bot.NearbyPlaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Google_Maps_Places_Bot
{
    public class NearbyPlacesApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl = "https://google-maps-places-production.up.railway.app";

        public NearbyPlacesApiClient()
        {
            _httpClient = new HttpClient();
        }

        public async Task<NearbyPlaces> GetNearbyPlacesAsync(double latitude, double longitude, double radius, string language)
        {
            var url = $"{_baseUrl}/api/NearbyPlaces/SearchNearbyPlaces?latitude={latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&" +
                $"longitude={longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&" +
                $"radius={radius.ToString(System.Globalization.CultureInfo.InvariantCulture)}&language={language}";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            Console.WriteLine("JSON RESPONSE:");
            Console.WriteLine(json);
            return JsonConvert.DeserializeObject<NearbyPlaces>(json);
        }
        public async Task AddToFavouritesAsync(string name, string placeId, string comment, string chatId)
        {
            var client = new HttpClient();
            var apiUrl = $"{_baseUrl}/api/NearbyPlaces/AddFavouritePlace";

            var data = new
            {
                Name = name,
                PlaceID = placeId,
                Comment = comment,
                ChatID = chatId
            };

            var content = new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(apiUrl, content);

            response.EnsureSuccessStatusCode();
        }
        public async Task<List<string>> GetFavouritesAsync(string chatId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/NearbyPlaces/GetFavourite?ChatID={chatId}");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<List<string>>(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Помилка отримання улюблених: {ex}");
                return null;
            }
        }
        public async Task<string> GetPhotoUriAsync(string placeId)
        {
            var url = $"{_baseUrl}/api/PhotoPlace/GetPhoto?placeId={placeId}";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var jsonObject = JsonConvert.DeserializeObject<JObject>(json);

            return jsonObject["photoUri"]?.ToString();
        }
    }
}
