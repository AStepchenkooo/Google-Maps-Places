using Google_Maps_Places_Bot.Models;
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
        private Dictionary<long, string> _waitingForType = new();
        private static Dictionary<string, PlaceInfo> _placesCache = new();
        private static HashSet<long> _waitingForRoute = new();
        private Dictionary<long, List<string>> _userSearchTypes = new();

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

                _locationCache[message.Chat.Id] = (lat, lon);  // Перезаписуємо локацію

                if (_waitingForRoute.Contains(message.Chat.Id))
                {
                    _waitingForRoute.Remove(message.Chat.Id);
                    await GenerateRouteAfterLocation(message.Chat.Id);
                    return;
                }

                // **Якщо маршрут не потрібен, показуємо головне меню**
                ReplyKeyboardMarkup mainMenu = new(new[]
                {
        new KeyboardButton[] { "Пошук місць поруч", "Вподобані місця" }
    })
                {
                    ResizeKeyboard = true,
                    OneTimeKeyboard = false
                };

                await botClient.SendTextMessageAsync(message.Chat.Id, "✅ Геолокацію отримано! Тепер обери наступну дію:", replyMarkup: mainMenu);

                await SendPlaceTypeSelection(message.Chat.Id);  // Вибір категорії пошуку місць
            }

            if (_waitingForRadius.ContainsKey(message.Chat.Id) && _waitingForRadius[message.Chat.Id])
            {
                if (int.TryParse(message.Text, out int radius))
                {
                    _waitingForRadius.Remove(message.Chat.Id);
                    var placeType = _waitingForType[message.Chat.Id];
                    _waitingForType.Remove(message.Chat.Id);

                    if (!_locationCache.ContainsKey(message.Chat.Id))
                    {
                        await botClient.SendTextMessageAsync(message.Chat.Id, "❌ Спочатку потрібно надіслати свою геолокацію!");
                        return;
                    }

                    var (lat, lon) = _locationCache[message.Chat.Id]; // Отримуємо координати

                    await botClient.SendTextMessageAsync(message.Chat.Id, $"🔍 Шукаємо {placeType} в радіусі {radius}м...");

                    var apiClient = new NearbyPlacesApiClient();
                    var result = await apiClient.GetNearbyPlacesAsync(lat, lon, radius, "uk", placeType);

                    if (result.results == null || result.results.Count() == 0)
                    {
                        await botClient.SendTextMessageAsync(message.Chat.Id, "❌ Нічого не знайдено.");
                    }
                    else
                    {
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

                        await botClient.SendTextMessageAsync(message.Chat.Id, placeText, replyMarkup: markup, parseMode: ParseMode.Html);
                    }
                }
                else
                {
                    await botClient.SendTextMessageAsync(message.Chat.Id, "❗ Введіть число (радіус у метрах), наприклад: 3000");
                }
            }
            if (_waitingForComment.TryGetValue(message.Chat.Id, out var savedPlace))
            {
                string comment = message.Text;

                var addToFavouriteAsync = new NearbyPlacesApiClient();
                await addToFavouriteAsync.AddToFavouritesAsync(savedPlace.name, savedPlace.place_id, comment, message.Chat.Id.ToString(), savedPlace.types.ToList());
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
                if (!_userSearchResults.ContainsKey(chatId))
                {
                    await botClient.SendTextMessageAsync(chatId, "❌ Дані для цього місця вже недоступні.");
                    return;
                }

                var index = int.Parse(callbackQuery.Data.Split('_')[1]);
                var place = _userSearchResults[chatId][index];

                // Отримуємо URL фото
                var apiClient = new NearbyPlacesApiClient();
                string photoUri = await apiClient.GetPhotoUriAsync(place.place_id);
                PlaceInfo placeDetails = apiClient.GetInfoAsync(place.place_id).Result;
                var latestReview = placeDetails.result.reviews?.OrderByDescending(r => r.time).FirstOrDefault();
                string basicInfo = $"📍 <b>{placeDetails.result.name}</b>\n" +
                   $"⭐ Рейтинг: {placeDetails.result.rating} (відгуків: {placeDetails.result.user_ratings_total})";

                var reviewText = placeDetails.result.reviews?.OrderByDescending(r => r.time).FirstOrDefault();
                string reviewInfo = reviewText != null
                    ? $"💬 <b>Останній відгук</b> ({reviewText.rating}⭐ від {reviewText.author_name}):\n\"{reviewText.text}\""
                    : "❌ Відгуки відсутні.";

                string addressInfo = $"📍 Адреса: {placeDetails.result.formatted_address}\n" +
                                     $"📞 Телефон: {placeDetails.result.formatted_phone_number}\n" +
                                     $"{(placeDetails.result.website != null ? $"🌐 <a href=\"{placeDetails.result.website}\">Сайт</a>\n" : "")}" +
                                     $"🔗 <a href=\"{placeDetails.result.url}\">Google Maps</a>";
                _placesCache.Add(place.place_id, placeDetails);
                InlineKeyboardMarkup detailsMarkup = new(new[]
                    {
                        new [] { InlineKeyboardButton.WithCallbackData("🗺 Отримати маршрут", $"route_{place.place_id}") },
                    });
                // **Перевіряємо, чи є фото * *
                if (!string.IsNullOrEmpty(photoUri))
                {
                    // **Перевіряємо довжину тексту**
                    if ((basicInfo.Length + reviewInfo.Length + addressInfo.Length) <= 1024)
                    {
                        await botClient.SendPhotoAsync(chatId, photo: photoUri, caption: basicInfo + "\n\n" + reviewInfo + "\n\n" + addressInfo, replyMarkup: detailsMarkup, parseMode: ParseMode.Html);
                    }
                    else
                    {
                        await botClient.SendPhotoAsync(chatId, photo: photoUri, caption: basicInfo, parseMode: ParseMode.Html);
                        await botClient.SendTextMessageAsync(chatId, reviewInfo, parseMode: ParseMode.Html);
                        await botClient.SendTextMessageAsync(chatId, addressInfo, replyMarkup: detailsMarkup, parseMode: ParseMode.Html);
                    }
                }
                else
                {
                    // **Якщо фото немає, надсилаємо текст окремо**
                    if ((basicInfo.Length + reviewInfo.Length + addressInfo.Length) <= 4096) // Загальний ліміт на текст у Telegram
                    {
                        await botClient.SendTextMessageAsync(chatId, basicInfo + "\n\n" + reviewInfo + "\n\n" + addressInfo, replyMarkup: detailsMarkup, parseMode: ParseMode.Html);
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(chatId, basicInfo, parseMode: ParseMode.Html);
                        await botClient.SendTextMessageAsync(chatId, reviewInfo, parseMode: ParseMode.Html);
                        await botClient.SendTextMessageAsync(chatId, addressInfo, replyMarkup: detailsMarkup, parseMode: ParseMode.Html);
                    }
                }
                await botClient.AnswerCallbackQueryAsync(callbackQuery.Id);

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
                    await addToFavouriteAsync.AddToFavouritesAsync(place.name, place.place_id, "—", chatId.ToString(), place.types.ToList());
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
                await botClient.AnswerCallbackQueryAsync(callbackQuery.Id);

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
                await botClient.AnswerCallbackQueryAsync(callbackQuery.Id);

            }
            if (callbackQuery.Data.StartsWith("search_"))
            {
                string placeType = callbackQuery.Data.Substring(7);

                if (!_userSearchTypes.ContainsKey(chatId))
                {
                    _userSearchTypes[chatId] = new List<string>();
                }

                _userSearchTypes[chatId].Add(placeType);  

                await botClient.SendTextMessageAsync(chatId, "📝 Введіть радіус пошуку в метрах (наприклад: 3000):");
                _waitingForRadius[chatId] = true;

                await botClient.AnswerCallbackQueryAsync(callbackQuery.Id);
            }
            if (callbackQuery.Data.StartsWith("route_favorites_"))
            {
                string placeId = callbackQuery.Data.Substring(16);

                // **Завжди запитуємо геолокацію**
                _waitingForRoute.Add(chatId);
                _waitingForPlaceId[chatId] = placeId;  // Тимчасово зберігаємо placeId

                await RequestLocation(chatId);
                await botClient.AnswerCallbackQueryAsync(callbackQuery.Id);
                return;
            }
            else if (callbackQuery.Data.StartsWith("route_"))
            {
                string placeId = callbackQuery.Data.Substring(6);
                var userLocation = _locationCache[chatId];  // Використовуємо останню локацію
                string origin = $"{userLocation.lat},{userLocation.lon}";
                string mapsUrl = GenerateRouteUrl(placeId, origin);

                await botClient.SendTextMessageAsync(chatId, $"🗺 <b>Маршрут до місця</b>:\n🔗 <a href=\"{mapsUrl}\">Google Maps</a>", parseMode: ParseMode.Html);
                await botClient.AnswerCallbackQueryAsync(callbackQuery.Id);
            }
        }

        private string GenerateRouteUrl(string placeId, string origin)
        {
            if (!_placesCache.ContainsKey(placeId)) return "❌ Дані місця не знайдені.";

            var placeDetails = _placesCache[placeId];
            string destination = $"{placeDetails.result.geometry.location.lat},{placeDetails.result.geometry.location.lng}";

            return $"https://www.google.com/maps/dir/{origin}/{destination}";
        }
        private async Task GenerateRouteAfterLocation(long chatId)
        {
            if (!_waitingForPlaceId.ContainsKey(chatId)) return;

            string placeId = _waitingForPlaceId[chatId];
            _waitingForPlaceId.Remove(chatId);
            _waitingForRoute.Remove(chatId); // Видаляємо маршрутне очікування

            var userLocation = _locationCache[chatId];
            string origin = $"{userLocation.lat},{userLocation.lon}";

            string mapsUrl = GenerateRouteUrl(placeId, origin);

            Console.WriteLine($"DEBUG: {mapsUrl}");
            _placesCache.Clear();
            await botClient.SendTextMessageAsync(chatId, $"🗺 <b>Маршрут до місця</b>:\n🔗 <a href=\"{mapsUrl}\">Google Maps</a>", parseMode: ParseMode.Html);
            await MenuKeyboard(chatId);  // Повертаємо користувача в головне меню
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
        private async Task RequestLocation(long ChatID)
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

            await botClient.SendTextMessageAsync(chatId: ChatID, text: "Натисніть кнопку нижче, щоб надіслати свою геолокацію:", replyMarkup: locationKeyboard);
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
        private async Task MenuKeyboard(long chatID)
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
            await botClient.SendTextMessageAsync(chatID, "Виберіть функцію", replyMarkup: replyKeyboardMarkup);
            return;
        }
        private async Task SendPlaceTypeSelection(long chatId)
        {
            var markup = new InlineKeyboardMarkup(new[]
            {
        new [] { InlineKeyboardButton.WithCallbackData("☕ Кафе", "search_cafe"), InlineKeyboardButton.WithCallbackData("💊 Аптека", "search_pharmacy") },
        new [] { InlineKeyboardButton.WithCallbackData("🌳 Парк", "search_park"), InlineKeyboardButton.WithCallbackData("🎭 Музей", "search_museum") },
        new [] { InlineKeyboardButton.WithCallbackData("🛍 Магазин", "search_store"), InlineKeyboardButton.WithCallbackData("🏥 Лікарня", "search_hospital") },
        new [] { InlineKeyboardButton.WithCallbackData("🏋️‍♂️ Спортзал", "search_gym"), InlineKeyboardButton.WithCallbackData("📮 Пошта", "search_post_office") },
        new [] { InlineKeyboardButton.WithCallbackData("🔌 Електроніка", "search_electronics_store"), InlineKeyboardButton.WithCallbackData("🎬 Кінотеатр", "search_movie_theater") }
    });

            await botClient.SendTextMessageAsync(chatId, "🔍 Обери тип місця:", replyMarkup: markup);
        }
        private async Task ShowFavoritesMenu(long chatId)
        {
            try
            {
                _userSearchResults.Remove(chatId);
                _userSearchIndex.Remove(chatId);
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

                foreach (var fav in favorites)
                {
                    var placeDetails = await apiClient.GetInfoAsync(fav.PlaceID);
                    string photoUri = await apiClient.GetPhotoUriAsync(fav.PlaceID);

                    string basicInfo = $"📍 <b>{fav.Name}</b>\n" +
                                       $"⭐ Рейтинг: {placeDetails.result.rating} (відгуків: {placeDetails.result.user_ratings_total})";

                    string reviewInfo = !string.IsNullOrEmpty(fav.Comment)
                        ? $"💬 <b>Твій коментар:</b> \"{fav.Comment}\""
                        : "❌ Коментар відсутній.";

                    string addressInfo = $"📍 Адреса: {placeDetails.result.formatted_address}\n" +
                                         $"📞 Телефон: {placeDetails.result.formatted_phone_number}\n" +
                                         $"{(placeDetails.result.website != null ? $"🌐 <a href=\"{placeDetails.result.website}\">Сайт</a>\n" : "")}" +
                                         $"🔗 <a href=\"{placeDetails.result.url}\">Google Maps</a>\n";
                    _placesCache.Add(fav.PlaceID, placeDetails);
                    Console.WriteLine($"Генеруємо кнопки: delete_{fav.PlaceID}");
                    InlineKeyboardMarkup markup = new(new[]
                    {
                        new [] { InlineKeyboardButton.WithCallbackData("🗺 Отримати маршрут", $"route_favorites_{fav.PlaceID}") },
                        new [] { 
                            InlineKeyboardButton.WithCallbackData("✏ Редагувати коментар", $"edit_{fav.PlaceID}"),
                            InlineKeyboardButton.WithCallbackData("❌ Видалити", $"delete_{fav.PlaceID}") }
                        });

                    // **Перевірка довжини тексту перед відправкою**
                    if ((basicInfo.Length + reviewInfo.Length + addressInfo.Length) <= 1024 && !string.IsNullOrEmpty(photoUri))
                    {
                        await botClient.SendPhotoAsync(chatId, photo: photoUri, caption: $"{basicInfo}\n\n{reviewInfo}\n\n{addressInfo}",
                                                       replyMarkup: markup, parseMode: ParseMode.Html);
                    }
                    else
                    {
                        // **Відправка частинами**
                        if (!string.IsNullOrEmpty(photoUri))
                            await botClient.SendPhotoAsync(chatId, photo: photoUri, caption: basicInfo, replyMarkup: markup, parseMode: ParseMode.Html);
                        else
                            await botClient.SendTextMessageAsync(chatId, basicInfo, replyMarkup: markup, parseMode: ParseMode.Html);

                        await botClient.SendTextMessageAsync(chatId, reviewInfo, parseMode: ParseMode.Html);
                        await botClient.SendTextMessageAsync(chatId, addressInfo, parseMode: ParseMode.Html);
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
