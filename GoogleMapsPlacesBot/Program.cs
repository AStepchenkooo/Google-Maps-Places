using Google_Maps_Places_Bot;
public class Program
{
    static async Task Main(string[] args)
    {
        GoogleMapsPlacesBot googleMapsPlacesBot = new GoogleMapsPlacesBot();
        googleMapsPlacesBot.Start();
    }
}