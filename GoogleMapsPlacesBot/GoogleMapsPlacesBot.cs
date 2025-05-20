using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Bot.NearbyPlaces;
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
            if (message.Type == MessageType.Location)
            {
                var lat = message.Location.Latitude;
                var lon = message.Location.Longitude;

                _locationCache[message.Chat.Id] = (lat, lon);
                _waitingForRadius[message.Chat.Id] = true;

                await botClient.SendTextMessageAsync(
                    message.Chat.Id,
                    "Введіть радіус пошуку в метрах (наприклад: 3000):"
                );
                
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


        }
        private async Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery)
        {
            var chatId = callbackQuery.Message.Chat.Id;

            if (!_userSearchResults.ContainsKey(chatId)) return;

            if (callbackQuery.Data.StartsWith("details_"))
            {
                var index = int.Parse(callbackQuery.Data.Split('_')[1]);
                var place = _userSearchResults[chatId][index];

                string text = $"<b>{place.name}</b>\n" +
                              $"🆔 Place ID: {place.place_id}\n" +
                              $"⭐ Рейтинг: {place.rating}\n" +
                              $"📍 Адреса: {place.vicinity}";

                await botClient.AnswerCallbackQueryAsync(callbackQuery.Id);
                await botClient.SendTextMessageAsync(
                    chatId,
                    text,
                    parseMode: ParseMode.Html
                );
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

    }
}
