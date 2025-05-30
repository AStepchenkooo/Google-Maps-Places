using GoggleMapsPlaces;
using Newtonsoft.Json.Linq;
namespace Goggle_Maps_Places.Clients
{
    public class PhotoPlaceClient
    {
        private static string _apikey;
        private static string _apihost;

        public PhotoPlaceClient()
        {
            _apikey = Constants.ApiKey;
            _apihost = Constants.ApiHostV2;
        }
        public async Task<string> PlacePhoto(string id)
        {
            var client1 = new HttpClient();
            var request1 = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri($"https://{_apihost}/v1/places/{id}"),
                Headers =
    {
        { "x-rapidapi-key", _apikey },
        { "x-rapidapi-host", _apihost },
        { "X-Goog-FieldMask", "photos" },
    },
            };
            string firstName = null;
            using (var response = await client1.SendAsync(request1))
            {
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadAsStringAsync();

                var json = JObject.Parse(body);

                firstName = json["photos"]?[0]?["name"]?.ToString();
            }

            var client = new HttpClient();
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri($"https://{_apihost}/v1/{firstName}/media?maxWidthPx=400&maxHeightPx=400&skipHttpRedirect=true"),
                Headers =
                {
                    { "x-rapidapi-key", _apikey },
                    { "x-rapidapi-host", _apihost },
                },
            };

            using (var response = await client.SendAsync(request))
            {
                try
                {
                    response.EnsureSuccessStatusCode();

                    // Отримуємо Content-Type для перевірки
                    var contentType = response.Content.Headers.ContentType?.MediaType;

                    // Обробка відповіді в залежності від типу вмісту

                        // Якщо JSON - виводимо в консоль
                        var jsonResponse = await response.Content.ReadAsStringAsync();
                        var json = JObject.Parse(jsonResponse);

                        // Отримуємо значення поля photoUri
                        string photoUri = json["photoUri"]?.ToString();
                        return photoUri;
                    
                }
                catch (HttpRequestException ex)
                {
                    return await response.Content.ReadAsStringAsync();      
                }
            }
        }
    }
}
