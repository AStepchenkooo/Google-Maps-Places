using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
