﻿using Npgsql;
using System.Data;
using System.Xml.Linq;

namespace GoggleMapsPlaces.DataBase
{
    public class FavouriteDB
    {
        NpgsqlConnection _connection = new NpgsqlConnection(Constants.Connect);

        public async Task InsertFavouritePlaceAsync(string name, string placeID, string comment, string chatID)
        {
            var sql = "INSERT INTO public.\"favouriteplaces\"(\"name\", \"placeid\", \"comment\", \"chatid\") " +
                      "VALUES (@Name, @PlaceID, @Comment, @ChatID)";

            await using var cmd = new NpgsqlCommand(sql, _connection);
            cmd.Parameters.AddWithValue("Name", name);
            cmd.Parameters.AddWithValue("PlaceID", placeID);
            cmd.Parameters.AddWithValue("Comment", comment);
            cmd.Parameters.AddWithValue("ChatID", chatID);

            if (_connection.State != ConnectionState.Open)
                await _connection.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
            await _connection.CloseAsync();
        }
        public async Task<List<(string Name, string Comment, string PlaceId)>> GetFavouritePlacesAsync(string chatID)
        {
            var places = new List<(string Name, string Comment, string PlaceId)>();
            var sql = "SELECT \"name\", \"comment\", \"placeid\" FROM public.\"favouriteplaces\" WHERE \"chatid\" = @chat_id";

            await using var cmd = new NpgsqlCommand(sql, _connection);

            if (_connection.State != ConnectionState.Open)
                await _connection.OpenAsync();

            cmd.Parameters.AddWithValue("@chat_id", chatID);
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var placeId = reader["placeid"].ToString();
                Console.WriteLine($"Отримано PlaceId з БД: {placeId}"); // Лог для перевірки
                places.Add((reader["name"].ToString(), reader["comment"].ToString(), placeId));
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
    }
}
