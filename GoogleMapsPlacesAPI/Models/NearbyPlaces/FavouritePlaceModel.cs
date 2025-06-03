namespace Goggle_Maps_Places.Models.NearbyPlaces
{
    public class FavouritePlaceModel
    {
        public string Name { get; set; }
        public string PlaceID { get; set; }
        public string Comment { get; set; }
        public string ChatID { get; set; }
        public List<string> PlaceTypes { get; set; }
    }

}
