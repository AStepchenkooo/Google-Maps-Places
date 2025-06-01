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
        public async Task<List<FavouritePlaceModel>> GetFavouritesAsync(string ChatID)
        {
            FavouriteDB np = new FavouriteDB();
            var result = await np.GetFavouritePlacesAsync(ChatID);

            return result.Select(f => new FavouritePlaceModel
            {
                Name = f.Name,
                PlaceID = f.PlaceId,
                Comment = f.Comment,
                ChatID = ChatID
            }).ToList();
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
        [HttpDelete]
        [ActionName("DeleteFavourite")]
        public async Task<IActionResult> DeleteFavourite(string chatId, string placeId)
        {
            Console.WriteLine($"Отримано запит на видалення: ChatID={chatId}, PlaceID={placeId}");
            try
            {
                FavouriteDB np = new FavouriteDB();
                bool success = await np.RemoveFavouriteAsync(chatId, placeId);

                if (success)
                    return Ok(new { message = "✅ Успішно видалено з улюблених!" });
                else
                    return BadRequest(new { message = "❌ Помилка при видаленні!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Помилка видалення: {ex}");
                return StatusCode(500, new { message = "❌ Внутрішня помилка сервера" });
            }
        }
        [HttpPut]
        [ActionName("EditFavourite")]
        public async Task<IActionResult> EditFavourite(string chatId, string placeId, string newComment)
        {
            try
            {
                FavouriteDB np = new FavouriteDB();
                bool success = await np.UpdateCommentAsync(chatId, placeId, newComment);

                if (success)
                    return Ok(new { message = "✅ Коментар оновлено!" });
                else
                    return BadRequest(new { message = "❌ Помилка при оновленні!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Помилка оновлення коментаря: {ex}");
                return StatusCode(500, new { message = "❌ Внутрішня помилка сервера" });
            }
        }
    }
}
