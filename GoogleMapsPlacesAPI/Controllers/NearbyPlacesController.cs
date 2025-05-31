using GoggleMapsPlaces.Clients;
using GoggleMapsPlaces.DataBase;
using GoggleMapsPlaces.Models.NearbyPlaces;
using Microsoft.AspNetCore.Mvc;
using Goggle_Maps_Places.Models.NearbyPlaces;
using GoggleMapsPlaces.Models.PlaceInfo;
namespace Goggle_Maps_Places.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class NearbyPlacesController : ControllerBase
    {
        private readonly ILogger<NearbyPlacesController> _logger;

        public NearbyPlacesController(ILogger<NearbyPlacesController> logger)
        {
            this._logger = logger;
        }
        [HttpGet]
        [ActionName("SearchNearbyPlaces")]
        public NearbyPlaces SearchNearbyPlaces(double latitude, double longitude, double radius, string language)
        {
            NearbyPlacesClient np = new NearbyPlacesClient();
            NearbyPlaces places = np.GetNearbyPlaces(latitude, longitude, radius, language).Result;
            return places;
        }
        [HttpGet]
        [ActionName("GetFavourite")]
        public async Task<List<(string Name, string Comment, string PlaceId)>> GetFavouritesAsync(string ChatID)
        {
            FavouriteDB np = new FavouriteDB();
            var result = np.GetFavouritePlacesAsync(ChatID).Result;
            Console.WriteLine("Перевіряємо список перед foreach...");
            Console.WriteLine(string.Join("\n", result.Select(f => $"Name: {f.Name}, PlaceId: {f.PlaceId}")));
            return result;
        }
        [HttpPost]
        [ActionName("AddFavouritePlace")]
        public async Task FavouriteAddAsync([FromBody] FavouritePlaceModel model)
        {
            FavouriteDB db = new FavouriteDB();
            await db.InsertFavouritePlaceAsync(model.Name, model.PlaceID, model.Comment, model.ChatID);
        }
        [HttpGet]
        [ActionName("PlaceInfo")]
        public PlaceInfo GetInfo(string id)
        {
            NearbyPlacesClient placeInfo = new NearbyPlacesClient();
            PlaceInfo result = placeInfo.GetInfo(id).Result;
            return result;
        }
    }
}
