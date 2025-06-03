using Npgsql;
using System.Data;
using System.Xml.Linq;
using Newtonsoft.Json;
using Goggle_Maps_Places.Models.NearbyPlaces;

namespace GoggleMapsPlaces.DataBase
{
    public class FavouriteDB
    {
        NpgsqlConnection _connection = new NpgsqlConnection(Constants.Connect);

        public async Task InsertFavouritePlaceAsync(string name, string placeID, string comment, string chatID, List<string> placeTypes)
        {
            var placeTypesJson = JsonConvert.SerializeObject(placeTypes); // **Серіалізуємо масив у JSON**

            var sql = "INSERT INTO public.\"favouriteplaces\"(\"name\", \"placeid\", \"comment\", \"chatid\", \"placeType\") " +
                      "VALUES (@Name, @PlaceID, @Comment, @ChatID, @PlaceType)";

            await using var cmd = new NpgsqlCommand(sql, _connection);
            cmd.Parameters.AddWithValue("Name", name);
            cmd.Parameters.AddWithValue("PlaceID", placeID);
            cmd.Parameters.AddWithValue("Comment", comment);
            cmd.Parameters.AddWithValue("ChatID", chatID);
            cmd.Parameters.AddWithValue("PlaceType", placeTypesJson);  // **Оновлено для JSONB**

            if (_connection.State != ConnectionState.Open)
                await _connection.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
            await _connection.CloseAsync();
        }
        public async Task<List<FavouritePlaceModel>> GetFavouritePlacesAsync(string chatID)
        {
            var places = new List<FavouritePlaceModel>();
            var sql = "SELECT \"name\", \"comment\", \"placeid\", \"placeType\" FROM public.\"favouriteplaces\" WHERE \"chatid\" = @chat_id";

            await using var cmd = new NpgsqlCommand(sql, _connection);

            if (_connection.State != ConnectionState.Open)
                await _connection.OpenAsync();

            cmd.Parameters.AddWithValue("@chat_id", chatID);
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var placeId = reader["placeid"].ToString();
                var placeTypeJson = reader["placeType"].ToString();
                var placeTypes = JsonConvert.DeserializeObject<List<string>>(placeTypeJson) ?? new List<string>();

                Console.WriteLine($"Отримано PlaceId: {placeId}, Типи місця: {string.Join(", ", placeTypes)}");

                places.Add(new FavouritePlaceModel
                {
                    Name = reader["name"].ToString(),
                    Comment = reader["comment"].ToString(),
                    PlaceID = placeId,
                    PlaceTypes = placeTypes
                });
            }

            await _connection.CloseAsync();
            return places;
        }
        public async Task<bool> RemoveFavouriteAsync(string chatId, string placeId)
        {
            var sql = "DELETE FROM public.\"favouriteplaces\" WHERE \"chatid\" = @chat_id AND \"placeid\" = @place_id";
            Console.WriteLine($"Отримано запит в базі на видалення: ChatID={chatId}, PlaceID={placeId}");
            await using var cmd = new NpgsqlCommand(sql, _connection);

            if (_connection.State != ConnectionState.Open)
                await _connection.OpenAsync();

            cmd.Parameters.AddWithValue("@chat_id", chatId);
            cmd.Parameters.AddWithValue("@place_id", placeId);

            int rowsAffected = await cmd.ExecuteNonQueryAsync();
            await _connection.CloseAsync();

            return rowsAffected > 0; 
        }
        public async Task<bool> UpdateCommentAsync(string chatId, string placeId, string newComment)
        {
            var checkSql = "SELECT COUNT(*) FROM public.\"favouriteplaces\" WHERE \"chatid\" = @chat_id AND \"placeid\" = @place_id";
            await using var checkCmd = new NpgsqlCommand(checkSql, _connection);

            checkCmd.Parameters.AddWithValue("@chat_id", chatId);
            checkCmd.Parameters.AddWithValue("@place_id", placeId);

            if (_connection.State != ConnectionState.Open)
                await _connection.OpenAsync();

            int count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
            Console.WriteLine($"🔍 Запис існує у БД: {count > 0}");

            if (count == 0)
            {
                Console.WriteLine($"❌ Місце {placeId} не знайдено для користувача {chatId}");
                return false;
            }

            var sql = "UPDATE public.\"favouriteplaces\" SET \"comment\" = @comment WHERE \"chatid\" = @chat_id AND \"placeid\" = @place_id";

            await using var cmd = new NpgsqlCommand(sql, _connection);

            if (_connection.State != ConnectionState.Open)
                await _connection.OpenAsync();

            cmd.Parameters.AddWithValue("@chat_id", chatId);
            cmd.Parameters.AddWithValue("@place_id", placeId);
            cmd.Parameters.AddWithValue("@comment", newComment);

            int rowsAffected = await cmd.ExecuteNonQueryAsync();
            await _connection.CloseAsync();

            return rowsAffected > 0; 
        }
    }
}
