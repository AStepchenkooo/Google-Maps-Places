using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Goggle_Maps_Places.Models.NearbyPlaces;
using Google_Maps_Places_Bot.Models;

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

        public async Task<NearbyPlaces> GetNearbyPlacesAsync(double latitude, double longitude, double radius, string language, string type)
        {
            var url = $"{_baseUrl}/api/NearbyPlaces/SearchNearbyPlaces?latitude={latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&" +
                $"longitude={longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&" +
                $"radius={radius.ToString(System.Globalization.CultureInfo.InvariantCulture)}&type={type}&language={language}";

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
        public async Task<List<FavouritePlaceModel>> GetFavouritesAsync(string chatId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/NearbyPlaces/GetFavourite?ChatID={chatId}");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                Console.WriteLine("JSON отриманий від API:");
                Console.WriteLine(json); // Логування JSON-відповіді

                var result = JsonConvert.DeserializeObject<List<FavouritePlaceModel>>(json);

                Console.WriteLine("Перевіряємо список в АПІ бота...");
                Console.WriteLine(string.Join("\n", result.Select(f => $"Name: {f.Name}, PlaceID: {f.PlaceID}")));

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Помилка отримання улюблених: {ex}");
                return null;
            }
        }
        public async Task<string> GetPhotoUriAsync(string placeId)
        {
            var url = $"{_baseUrl}/api/PhotoPlace/GetPhoto?id={placeId}";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var photoUri = await response.Content.ReadAsStringAsync();

            return photoUri;
        }
        public async Task<PlaceInfo> GetInfoAsync(string id)
        {
            var url = $"{_baseUrl}/api/NearbyPlaces/PlaceInfo?id={id}";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            Console.WriteLine("JSON RESPONSE:");
            Console.WriteLine(json);
            return JsonConvert.DeserializeObject<PlaceInfo>(json);
        }
        public async Task<bool> RemoveFavouriteAsync(string chatId, string placeId)
        {
            var url = $"{_baseUrl}/api/NearbyPlaces/DeleteFavourite?chatId={chatId}&placeId={placeId}";
            Console.WriteLine($"Отримано запит на видалення: ChatID={chatId}, PlaceID={placeId}");
            var response = await _httpClient.DeleteAsync(url);
            return response.IsSuccessStatusCode;
        }
        public async Task<bool> EditFavouriteAsync(string chatId, string placeId, string newComment)
        {
            var url = $"{_baseUrl}/api/NearbyPlaces/EditFavourite";

            var payload = new EditFavouriteRequest
            {
                ChatId = chatId,
                PlaceId = placeId,
                NewComment = newComment
            };

            var jsonPayload = JsonConvert.SerializeObject(payload);
            Console.WriteLine($"📤 JSON-запит перед відправкою: {jsonPayload}");

            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            var response = await _httpClient.PutAsync(url, content);
            Console.WriteLine($"🔍 Відповідь сервера: {response.StatusCode}");
            Console.WriteLine($"🔍 Текст відповіді: {await response.Content.ReadAsStringAsync()}");

            return response.IsSuccessStatusCode;
        }
    }
}
