using Npgsql;
using System.Data;

namespace Goggle_Maps_Places.DataBase
{
    public class FavouriteDB
    {
        NpgsqlConnection _connection = new NpgsqlConnection(Constants.Connect);

        public async Task InsertFavouritePlaceAsync(string Name, string PlaceID, string Comment)
        {
            var sql = "INSERT INTO public.\"FavouritePlaces\"(\"Name\",\"PlaceID\",\"Comment\")" +
                "VALUES (@Name, @PlaceID, @Comment)";
            NpgsqlCommand cmd = new NpgsqlCommand(sql, _connection);

            cmd.Parameters.AddWithValue("Name", Name);
            cmd.Parameters.AddWithValue("PlaceID", PlaceID);
            cmd.Parameters.AddWithValue("Comment", Comment);

            await _connection.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
            await _connection.CloseAsync();

            Console.WriteLine("\nKUKU\n");
        }
        public async Task<List<string>> GetFavouritePlacesAsync()
        {
            var places = new List<string>();
            var sql = "SELECT \"Name\",\"Comment\" FROM public.\"FavouritePlaces\"";

            await using var cmd = new NpgsqlCommand(sql, _connection);

            if (_connection.State != ConnectionState.Open)
                await _connection.OpenAsync();

            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                places.Add($"Place: {reader["Name"]}; Comment: {reader["Comment"]}");
            }

            await _connection.CloseAsync();

            return places;
        }

    }
}
