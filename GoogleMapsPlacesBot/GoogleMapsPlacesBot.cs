using Bot.NearbyPlaces;
using GoggleMapsPlaces.Models.PlaceInfo;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Google_Maps_Places_Bot
{
    internal class GoogleMapsPlacesBot
    {
        TelegramBotClient botClient = new TelegramBotClient("8044069877:AAFLrXir_Ft43u6xHfI2U8njcEgfOUw5O_o");
        CancellationToken cancellationToken = new CancellationToken();
        ReceiverOptions receiverOptions = new ReceiverOptions { AllowedUpdates = { } };
        private Dictionary<long, (double lat, double lon)> _locationCache = new();
        private Dictionary<long, bool> _waitingForRadius = new();
        private Dictionary<long, List<Result>> _userSearchResults = new();
        private Dictionary<long, int> _userSearchIndex = new();
        private Dictionary<long, Result> _waitingForComment = new();
        private Dictionary<long, string> _waitingForPlaceId = new();

        public async Task Start()
        {
            botClient.StartReceiving(HandlerUpdateAsync, HandlerError, receiverOptions, cancellationToken);
            var botMe = await botClient.GetMeAsync();
        }

        private Task HandlerError(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var ErrorMessage = exception switch
            {
                ApiRequestException apiRequestException => $"Помилка в телеграм бот АПІ: \n{apiRequestException.ErrorCode}" +
                $"{apiRequestException.Message}",
                _ => exception.ToString()
            };
            Console.WriteLine(ErrorMessage);
            return Task.CompletedTask;
        }

        private async Task HandlerUpdateAsync(ITelegramBotClient client, Update update, CancellationToken token)
        {
            if (update.Type == UpdateType.Message && (update.Message.Text != null || update.Message.Location != null))
            {
                await HandlerMessageAsync(botClient, update.Message);
            }
            else if (update.Type == UpdateType.CallbackQuery)
            {
                await HandleCallbackQueryAsync(client, update.CallbackQuery);
            }
        }

        private async Task HandlerMessageAsync(ITelegramBotClient botClient, Message message)
        {
            if (message.Text == "/start")
            {
                await MenuKeyboard(message);
                return;
            }
            if (message.Text == "Пошук місць поруч")
            {
                await RequestLocation(message);
                return;
            }
            if (message.Text == "Вподобані місця")
            {
                await ShowFavoritesMenu(message.Chat.Id);
                return;
            }
            if (message.Type == MessageType.Location)
            {
                var lat = message.Location.Latitude;
                var lon = message.Location.Longitude;

                _locationCache[message.Chat.Id] = (lat, lon);
                _waitingForRadius[message.Chat.Id] = true;

                // Використовуємо ту саму клавіатуру, що і в MenuKeyboard
                await botClient.SendTextMessageAsync(
                    message.Chat.Id,
                    "Введіть радіус пошуку в метрах (наприклад: 3000):",
                    replyMarkup: new ReplyKeyboardMarkup(new[]
                    {
            new KeyboardButton[] { "Пошук місць поруч", "Вподобані місця" }
                    })
                    { ResizeKeyboard = true });
                return;
            }
            if (message.Text != null && _waitingForRadius.TryGetValue(message.Chat.Id, out var waiting) && waiting)
            {
                if (double.TryParse(message.Text, out double radius))
                {
                    _waitingForRadius[message.Chat.Id] = false;
                    var (lat, lon) = _locationCache[message.Chat.Id];

                    await botClient.SendTextMessageAsync(
                        message.Chat.Id,
                        $"🔍 Шукаємо місця в радіусі {radius}м від {lat}, {lon}..."
                    );

                    // Виклик API
                    var apiClient = new NearbyPlacesApiClient();
                    var result = await apiClient.GetNearbyPlacesAsync(lat, lon, radius, "uk");

                    if (result.results == null || result.results.Count() == 0)
                    {
                        await botClient.SendTextMessageAsync(message.Chat.Id, "Нічого не знайдено 😢");
                    }
                    else
                    {
                        // Кешуємо результати для користувача
                        _userSearchResults[message.Chat.Id] = result.results.ToList();
                        _userSearchIndex[message.Chat.Id] = 0;

                        var place = result.results[0];
                        var placeText = $"📍 <b>{place.name}</b>\n⭐ Рейтинг: {place.rating}\n📍 Адреса: {place.vicinity}";

                        InlineKeyboardMarkup markup = new(
                            new[]
                            {
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData("Детальніше", $"details_0"),
                        InlineKeyboardButton.WithCallbackData("Наступне", "next")
                    },
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData("❤️ Додати до улюблених", $"addfav_0")
                    }

                            });

                        await botClient.SendTextMessageAsync(
                            chatId: message.Chat.Id,
                            text: placeText,
                            replyMarkup: markup,
                            parseMode: ParseMode.Html
                        );
                    }

                    _waitingForRadius.Remove(message.Chat.Id);
                }
                else
                {
                    await botClient.SendTextMessageAsync(
                        message.Chat.Id,
                        "❗ Введіть число (радіус у метрах), наприклад: 3000"
                    );
                }

                return;
            }
            if (_waitingForComment.TryGetValue(message.Chat.Id, out var savedPlace))
            {
                string comment = message.Text;

                var addToFavouriteAsync = new NearbyPlacesApiClient();
                await addToFavouriteAsync.AddToFavouritesAsync(savedPlace.name, savedPlace.place_id, comment, message.Chat.Id.ToString());
                await botClient.SendTextMessageAsync(message.Chat.Id, "✅ Додано в улюблені з коментарем");

                _waitingForComment.Remove(message.Chat.Id);
                return;
            }
            if (_waitingForPlaceId.TryGetValue(message.Chat.Id, out var placeId))
            {
                string newComment = message.Text;

                var apiClient = new NearbyPlacesApiClient();
                bool success = await apiClient.EditFavouriteAsync(message.Chat.Id.ToString(), placeId, newComment);

                if (success)
                    await botClient.SendTextMessageAsync(message.Chat.Id, "✅ Коментар успішно оновлено!");
                else
                    await botClient.SendTextMessageAsync(message.Chat.Id, "❌ Помилка при оновленні!");

                _waitingForPlaceId.Remove(message.Chat.Id);
            }


        }
        private async Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery)
        {
            Console.WriteLine($"Отримано CallbackData: {callbackQuery.Data}");
            var chatId = callbackQuery.Message.Chat.Id;

            if (callbackQuery.Data.StartsWith("details_"))
            {
                if (!_userSearchResults.ContainsKey(chatId)) return;

                var index = int.Parse(callbackQuery.Data.Split('_')[1]);
                var place = _userSearchResults[chatId][index];

                // Отримуємо URL фото
                var apiClient = new NearbyPlacesApiClient();
                string photoUri = await apiClient.GetPhotoUriAsync(place.place_id);
                PlaceInfo placeDetails = apiClient.GetInfoAsync(place.place_id).Result;
                var latestReview = placeDetails.result.reviews?.OrderByDescending(r => r.time).FirstOrDefault();
                string reviewText = latestReview != null ?
                    $"💬 <b>Останній відгук</b> ({latestReview.rating}⭐ від {latestReview.author_name}):\n\"{latestReview.text}\"\n" :
                    "❌ Відгуки відсутні.\n";

                string text = $"📍 <b>{placeDetails.result.name}</b>\n" +
              $"⭐ Рейтинг: {placeDetails.result.rating} (відгуків: {placeDetails.result.user_ratings_total})\n" +
              $"{reviewText}\n" +
              $"📍 Адреса: {placeDetails.result.formatted_address}\n" +
              $"📞 Телефон: {placeDetails.result.formatted_phone_number}\n" +
              $"{(placeDetails.result.website != null ? $"🌐 <a href=\"{placeDetails.result.website}\">Сайт</a>\n" : "")}" +
              $"{(placeDetails.result.opening_hours?.weekday_text != null ? $"🕒 Графік: \n{string.Join("\n\t", placeDetails.result.opening_hours.weekday_text)}\n" : "❌ Графік роботи недоступний.\n")}" +
              $"🔗 <a href=\"{placeDetails.result.url}\">Google Maps</a>\n";




                await botClient.AnswerCallbackQueryAsync(callbackQuery.Id);

                if (!string.IsNullOrEmpty(photoUri))
                {
                    await botClient.SendPhotoAsync(
                        chatId,
                        photo: photoUri,
                        caption: text,
                        parseMode: ParseMode.Html
                    );
                }
                else
                {
                    await botClient.SendTextMessageAsync(chatId, text, parseMode: ParseMode.Html);
                }
            }
            else if (callbackQuery.Data == "next")
            {
                var index = _userSearchIndex[chatId] + 1;
                var places = _userSearchResults[chatId];

                if (index >= places.Count)
                {
                    await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Це останнє місце 😅");
                    return;
                }

                _userSearchIndex[chatId] = index;
                var place = places[index];

                var text = $"📍 <b>{place.name}</b>\n⭐ Рейтинг: {place.rating}\n📍 Адреса: {place.vicinity}";

                InlineKeyboardMarkup markup = new(
                    new[]
                    {
                new []
                {
                    InlineKeyboardButton.WithCallbackData("Детальніше", $"details_{index}"),
                    InlineKeyboardButton.WithCallbackData("Наступне", "next")
                },
                new []
                {
                    InlineKeyboardButton.WithCallbackData("❤️ Додати до улюблених", $"addfav_{index}")
                }
                    });

                await botClient.AnswerCallbackQueryAsync(callbackQuery.Id);
                await botClient.SendTextMessageAsync(
                    chatId,
                    text,
                    replyMarkup: markup,
                    parseMode: ParseMode.Html
                );
            }
            else if (callbackQuery.Data.StartsWith("addfav_"))
            {
                var index = int.Parse(callbackQuery.Data.Split('_')[1]);
                var place = _userSearchResults[chatId][index];

                _waitingForComment[chatId] = place;

                var markup = new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("Пропустити", "skip_comment") }
                });

                await botClient.SendTextMessageAsync(
                    chatId,
                    "📝 Бажаєш залишити коментар до обраного місця? Введи його повідомленням або натисни «Пропустити».",
                    replyMarkup: markup
                );

                await botClient.AnswerCallbackQueryAsync(callbackQuery.Id);
            }
            if (callbackQuery.Data == "skip_comment")
            {
                if (_waitingForComment.TryGetValue(chatId, out var place))
                {
                    var addToFavouriteAsync = new NearbyPlacesApiClient();
                    await addToFavouriteAsync.AddToFavouritesAsync(place.name, place.place_id, "—", chatId.ToString());
                    await botClient.SendTextMessageAsync(chatId, "✅ Додано в улюблені без коментаря");
                    _waitingForComment.Remove(chatId);
                }

                await botClient.AnswerCallbackQueryAsync(callbackQuery.Id);
            }
            if (callbackQuery.Data.StartsWith("edit_"))
            {
                var placeId = callbackQuery.Data.Split('_')[1];

                // Запитуємо у користувача новий коментар
                await botClient.SendTextMessageAsync(
                    chatId,
                    $"✏ Введіть новий коментар для місця з ID {placeId}:"
                );

                _waitingForPlaceId[chatId] = placeId; 
            }
            if (callbackQuery.Data.StartsWith("delete_"))
            {
                Console.WriteLine($"➡ Виконується DELETE для {callbackQuery.Data}");
                var placeId = callbackQuery.Data.Split('_')[1];
                Console.WriteLine($"Видалення місця з ID: {placeId}");
                var apiClient = new NearbyPlacesApiClient();
                bool success = await apiClient.RemoveFavouriteAsync(chatId.ToString(), placeId);

                if (success)
                    await botClient.SendTextMessageAsync(chatId, "✅ Місце видалено з улюблених!");
                else
                    await botClient.SendTextMessageAsync(chatId, "❌ Помилка при видаленні!");
            }



        }

        private async Task RequestLocation(Message message)
        {
            ReplyKeyboardMarkup locationKeyboard = new
                (
                    new[]
                    {
                            new KeyboardButton("Надіслати мою геолокацію") { RequestLocation = true }
                    }
                )
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = true
            };

            await botClient.SendTextMessageAsync(chatId: message.Chat.Id, text: "Натисніть кнопку нижче, щоб надіслати свою геолокацію:", replyMarkup: locationKeyboard);
            return;
        }

        private async Task MenuKeyboard(Message message)
        {
            ReplyKeyboardMarkup replyKeyboardMarkup = new
                (
                    new[]
                    {
                        new KeyboardButton[]{"Пошук місць поруч", "Вподобані місця"}
                    }
                )
            {
                ResizeKeyboard = true
            };
            await botClient.SendTextMessageAsync(message.Chat.Id, "Виберіть функцію", replyMarkup: replyKeyboardMarkup);
            return;
        }
        private async Task ShowFavoritesMenu(long chatId)
        {
            try
            {
                var apiClient = new NearbyPlacesApiClient();
                var favorites = await apiClient.GetFavouritesAsync(chatId.ToString());

                var menu = new ReplyKeyboardMarkup(new[]
                    {
                        new KeyboardButton[] { "Пошук місць поруч", "Вподобані місця" }
                    })
                {
                    ResizeKeyboard = true
                };


                if (favorites == null || !favorites.Any())
                {
                    await botClient.SendTextMessageAsync(
                        chatId,
                        "У вас поки немає улюблених місць ❤️",
                        replyMarkup: menu);
                    return;
                }
                Console.WriteLine("Перевіряємо улюблені місця перед виводом:");
                foreach (var fav in favorites)
                {
                    Console.WriteLine($"Name: {fav.Name}, PlaceID: {fav.PlaceID}");
                }
                foreach (var fav in favorites)
                {
                    var placeDetails = await apiClient.GetInfoAsync(fav.PlaceID); // PlaceId тепер у FavouritePlaceModel
                    string photoUri = await apiClient.GetPhotoUriAsync(fav.PlaceID);

                    string text = $"📍 <b>{fav.Name}</b>\n" +
                                  $"⭐ Рейтинг: {placeDetails.result.rating} (відгуків: {placeDetails.result.user_ratings_total})\n" +
                                  $"💬 <b>Твій коментар:</b> \"{fav.Comment}\"\n" +
                                  $"📍 Адреса: {placeDetails.result.formatted_address}\n" +
                                  $"📞 Телефон: {placeDetails.result.formatted_phone_number}\n" +
                                  $"{(placeDetails.result.website != null ? $"🌐 <a href=\"{placeDetails.result.website}\">Сайт</a>\n" : "")}" +
                                  $"{(placeDetails.result.opening_hours?.weekday_text != null ? $"🕒 Графік:\n{string.Join("\n\t", placeDetails.result.opening_hours.weekday_text)}\n" : "❌ Графік роботи недоступний.\n")}" +
                                  $"🔗 <a href=\"{placeDetails.result.url}\">Google Maps</a>\n";
                    Console.WriteLine($"Генеруємо кнопки: delete_{fav.PlaceID}");
                    InlineKeyboardMarkup markup = new(
                    new[]
                        {
                        new []
                        {
                            InlineKeyboardButton.WithCallbackData("✏ Редагувати коментар", $"edit_{fav.PlaceID}"),
                            InlineKeyboardButton.WithCallbackData("❌ Видалити", $"delete_{fav.PlaceID}")
                        }
                    });
                    if (!string.IsNullOrEmpty(photoUri))
                    {
                        await botClient.SendPhotoAsync(
                            chatId,
                            photo: photoUri,
                            caption: text,
                            replyMarkup: markup,
                            parseMode: ParseMode.Html
                        );
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(chatId, text, replyMarkup: markup, parseMode: ParseMode.Html);
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Помилка отримання улюблених місць: {ex}");
                await botClient.SendTextMessageAsync(
                    chatId,
                    "❌ Сталася помилка при отриманні списку улюблених місць",
                    replyMarkup: new ReplyKeyboardMarkup(new[]
                    {
                new KeyboardButton[] { "Пошук місць поруч", "Вподобані місця" }
                    })
                    { ResizeKeyboard = true });
            }
        }
    }
}
