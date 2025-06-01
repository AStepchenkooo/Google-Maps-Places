using Newtonsoft.Json;

namespace Goggle_Maps_Places.Models.NearbyPlaces
{
    public class EditFavouriteRequest
    {
        [JsonProperty("chatId")]
        public string ChatId { get; set; }

        [JsonProperty("placeId")]
        public string PlaceId { get; set; }

        [JsonProperty("newComment")]
        public string NewComment { get; set; }
    }
}
