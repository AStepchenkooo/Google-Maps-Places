using GoggleMapsPlaces.Models.NearbyPlaces;
using Newtonsoft.Json;
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
                $"radius={radius.ToString(System.Globalization.CultureInfo.InvariantCulture)}&language={radius}";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            Console.WriteLine("JSON RESPONSE:");
            Console.WriteLine(json);
            return JsonConvert.DeserializeObject<NearbyPlaces>(json);
        }
    }
}
