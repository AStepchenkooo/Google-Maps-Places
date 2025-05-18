using Goggle_Maps_Places.Models.NearbyPlaces;
using Newtonsoft.Json;

namespace Goggle_Maps_Places.Clients
{
    public class NearbyPlacesClient
    {
        private static string _address;
        private static string _apikey;
        private static string _apihost;

        public NearbyPlacesClient()
        {
            _address = Constants.Address;
            _apikey = Constants.ApiKey;
            _apihost = Constants.ApiHost;
        }
        public async Task<NearbyPlaces> GetNearbyPlaces(double latitude, double longitude, double radius, string language)
        {
            var client = new HttpClient();

            var requestUri = $"{_address}location={latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)},{longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&radius={radius}&language={language}";

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(requestUri),
                Headers =
        {
            { "x-rapidapi-key", _apikey },
            { "x-rapidapi-host", _apihost }
        }
            };

            using (var response = await client.SendAsync(request))
            {
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadAsStringAsync();
                Console.WriteLine(body);
                var result = JsonConvert.DeserializeObject<NearbyPlaces>(body);
                return result;
            }
        }
    }
}
