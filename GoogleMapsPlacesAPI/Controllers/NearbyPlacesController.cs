using GoggleMapsPlaces.Clients;
using GoggleMapsPlaces.DataBase;
using GoggleMapsPlaces.Models.NearbyPlaces;
using Microsoft.AspNetCore.Mvc;

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
        public async Task<List<string>> GetFavouritesAsync()
        {
            FavouriteDB np = new FavouriteDB();
            return np.GetFavouritePlacesAsync().Result;
        }
        [HttpPost]
        [ActionName("FavouriteAdd")]
        public async Task FavouriteAddAsync(string Name, string PlaceID, string Comment)
        {
            FavouriteDB db = new FavouriteDB();
            await db.InsertFavouritePlaceAsync(Name, PlaceID, Comment);
        }
    }
}
