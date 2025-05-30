using Goggle_Maps_Places.Clients;
using Microsoft.AspNetCore.Mvc;

namespace Goggle_Maps_Places.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class PhotoPlaceController : ControllerBase
    {
        private readonly ILogger<PhotoPlaceController> _logger;

        public PhotoPlaceController(ILogger<PhotoPlaceController> logger)
        {
            this._logger = logger;
        }

        [HttpGet]
        [ActionName("GetPhoto")]
        public string GetPhoto([FromQuery] string id) // Додаємо FromQuery
        {
            if (string.IsNullOrEmpty(id))
            {
                return "Помилка: id відсутній!";
            }

            PhotoPlaceClient client = new PhotoPlaceClient();
            var result = client.PlacePhoto(id).Result;

            _logger.LogInformation($"Отримано photoUri: {result}");

            return result ?? "Помилка отримання фото!";
        }
    }
}
