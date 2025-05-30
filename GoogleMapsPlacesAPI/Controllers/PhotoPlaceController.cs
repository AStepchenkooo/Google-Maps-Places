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
        public string GetPhoto(string id)
        {
            PhotoPlaceClient client = new PhotoPlaceClient();
            return client.PlacePhoto(id).Result;
        }
    }
}
