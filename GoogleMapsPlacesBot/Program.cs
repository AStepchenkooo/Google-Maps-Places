using Google_Maps_Places_Bot;
using Newtonsoft.Json.Linq;

public class Program
{
    static async Task Main(string[] args)
    {
        GoogleMapsPlacesBot googleMapsPlacesBot = new GoogleMapsPlacesBot();
        googleMapsPlacesBot.Start();
        await Task.Delay(-1);
    }
}