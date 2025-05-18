using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Goggle_Maps_Places.Clients;
using Goggle_Maps_Places.Models;
using Goggle_Maps_Places.Models.NearbyPlaces;
namespace Google_Maps_Places_Bot
{
    internal class GoogleMapsPlacesBot
    {
        TelegramBotClient botClient = new TelegramBotClient("8044069877:AAFLrXir_Ft43u6xHfI2U8njcEgfOUw5O_o");
        CancellationToken cancellationToken = new CancellationToken();
        ReceiverOptions receiverOptions = new ReceiverOptions { AllowedUpdates = { } };
        private Dictionary<long, (double lat, double lon)> _locationCache = new();
        private Dictionary<long, bool> _waitingForRadius = new();

        public async Task Start()
        {
            botClient.StartReceiving(HandlerUpdateAsync, HandlerError, receiverOptions, cancellationToken);
            var botMe = await botClient.GetMeAsync();
            Console.WriteLine($"Бот {botMe.Username} почав працювати");
            Console.ReadKey();

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
                Console.WriteLine($"Отримано локацію:\nШирота: {message.Location.Latitude}\nДовгота: {message.Location.Longitude}");
                
                await GetNearby(message);
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

                    // Тут виклик API
                    var client = new NearbyPlacesClient();
                    NearbyPlaces result = await client.GetNearbyPlaces(lat, lon, radius, "uk");

                    // Виводимо результат (спрощено)
                    if (result.results == null || result.results.Count() == 0)
                    {
                        await botClient.SendTextMessageAsync(message.Chat.Id, "Нічого не знайдено 😢");
                    }
                    else
                    {
                        var msg = "📍 Ось деякі місця:\n";
                        for (int i = 0; i < result.results.Count(); i++)
                        {
                            msg += $"{i + 1}. {result.results[i].name}\n";
                        }

                        await botClient.SendTextMessageAsync(message.Chat.Id, msg);
                    }
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

        private async Task GetNearby(Message message)
        {
            var client = new NearbyPlacesClient();

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
