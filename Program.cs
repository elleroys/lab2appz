using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace WeatherTelegramBot
{
    public class Program
    {
        private static ITelegramBotClient botClient;
        private static string botToken = "7199893287:AAGKYn5GlvNzGg8uzFFSqZIQ4orCZ-AZMuI";
        private static string openWeatherMapApiKey = "f51adc2e93d569ef135231e1662de2fc";
        private static HttpClient httpClient = new HttpClient();
        private static string defaultUnit = "metric";


        public static async Task Main(string[] args)
        {
            botClient = new TelegramBotClient(botToken);

            var cts = new CancellationTokenSource();

            await SetBotCommandsAsync(botClient, cts.Token);

            botClient.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                new ReceiverOptions { AllowedUpdates = { } },
                cancellationToken: cts.Token
            );

            var me = await botClient.GetMeAsync();
            Console.WriteLine($"Start listening for @{me.Username}");
            Console.ReadLine();

            cts.Cancel();
        }

        private static async Task SetBotCommandsAsync(ITelegramBotClient botClient, CancellationToken cancellationToken)
        {
            var commands = new[]
            {
                new BotCommand { Command = "help", Description = "Show help information" },
                new BotCommand { Command = "unit", Description = "Select unit" }
            };

            await botClient.SetMyCommandsAsync(commands, cancellationToken: cancellationToken);
        }

        private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Type == UpdateType.Message && update.Message != null)
            {
                var message = update.Message;
                if (message.Type == MessageType.Text)
                {
                    if (message.Text == "/start")
                    {
                        await SendWelcomeMessage(message.Chat.Id, cancellationToken);
                        await SendUnitSelectionKeyboard(message.Chat.Id);
                    }
                    else if (message.Text.ToLower() == "/help")
                    {
                        await SendHelpMessageAsync(botClient, message.Chat.Id, cancellationToken);
                    }
                    else if (message.Text.ToLower() == "metric" || message.Text.ToLower() == "imperial")
                    {
                        defaultUnit = message.Text.ToLower();
                        await botClient.SendTextMessageAsync(message.Chat.Id, $"Selected units: {defaultUnit}", cancellationToken: cancellationToken);
                        await SendLocationRequestKeyboard(message.Chat.Id);
                    }
                    else
                    {
                        await HandleTextMessageAsync(botClient, message, cancellationToken);
                    }
                }
                else if (message.Type == MessageType.Location)
                {
                    await HandleLocationMessageAsync(botClient, message, cancellationToken);
                }
            }
        }

        private static async Task SendWelcomeMessage(long chatId, CancellationToken cancellationToken)
        {
            var welcomeMessage = "Welcome to the Weather Bot! You can use the following commands:\n" +
                                 "/unit - Select unit\n" +
                                 "/help - Show help information\n" +
                                 "You can also type the name of a city or send you geolocation to get the current weather information.";

            await botClient.SendTextMessageAsync(chatId, welcomeMessage, cancellationToken: cancellationToken);
        }

        private static async Task SendUnitSelectionKeyboard(long chatId)
        {
            var replyKeyboardMarkup = new ReplyKeyboardMarkup(new[]
            {
                new []
                {
                    new KeyboardButton("Metric"),
                    new KeyboardButton("Imperial")
                }
            });

            await botClient.SendTextMessageAsync(chatId, "Select units:", replyMarkup: replyKeyboardMarkup);
        }

        private static async Task SendLocationRequestKeyboard(long chatId)
        {
            var replyKeyboardMarkup = new ReplyKeyboardMarkup(new[]
            {
                new []
                {
                    new KeyboardButton("Send geolocation") { RequestLocation = true }
                }
            });

            await botClient.SendTextMessageAsync(chatId, "Send your geolocation or write the city in which you want to know the weather:", replyMarkup: replyKeyboardMarkup);
        }

        private static async Task SendHelpMessageAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
        {
            var helpMessage = "Here are the commands you can use:\n" +
                              "/unit - Select unit\n" +
                              "/help - Show help information\n" +
                              "You can also type the name of a city to get the current weather information.";

            await botClient.SendTextMessageAsync(chatId, helpMessage, cancellationToken: cancellationToken);
        }

        private static async Task HandleTextMessageAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            var chatId = message.Chat.Id;
            var messageText = message.Text;

            if (messageText.ToLower() == "/unit")
            {
                await SendUnitSelectionKeyboard(chatId);
            }
            else
            {
                var weatherInfo = await GetWeatherByCityAsync(messageText);
                await botClient.SendTextMessageAsync(chatId, weatherInfo, cancellationToken: cancellationToken);
            }
        }

        private static async Task HandleLocationMessageAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            var chatId = message.Chat.Id;
            var location = message.Location;

            if (location != null)
            {
                var weatherInfo = await GetWeatherByCoordinatesAsync(location.Latitude, location.Longitude);
                await botClient.SendTextMessageAsync(chatId, weatherInfo, cancellationToken: cancellationToken);
            }
            else
            {
                await botClient.SendTextMessageAsync(chatId, "Couldn't get location data.", cancellationToken: cancellationToken);
            }
        }

        private static async Task<string> GetWeatherByCityAsync(string city)
        {
            try
            {
                var response = await httpClient.GetStringAsync($"http://api.openweathermap.org/data/2.5/weather?q={city}&appid={openWeatherMapApiKey}&units={defaultUnit}");
                var data = JObject.Parse(response);
                return FormatWeatherResponse(data);
            }
            catch (HttpRequestException ex)
            {
                return $"Error communicating with the weather service: {ex.Message}";
            }
            catch (Newtonsoft.Json.JsonException ex)
            {
                return $"Error parsing weather data: {ex.Message}";
            }
            catch (Exception ex)
            {
                return $"An error occurred while retrieving weather data: {ex.Message}";
            }
        }

        private static async Task<string> GetWeatherByCoordinatesAsync(double latitude, double longitude)
        {
            try
            {
                var response = await httpClient.GetStringAsync($"http://api.openweathermap.org/data/2.5/weather?lat={latitude}&lon={longitude}&appid={openWeatherMapApiKey}&units={defaultUnit}");
                var data = JObject.Parse(response);
                return FormatWeatherResponse(data);
            }
            catch (HttpRequestException ex)
            {
                return $"Error communicating with the weather service: {ex.Message}";
            }
            catch (Newtonsoft.Json.JsonException ex)
            {
                return $"Error parsing weather data: {ex.Message}";
            }
            catch (Exception ex)
            {
                return $"An error occurred while retrieving weather data: {ex.Message}";
            }
        }

        private static string FormatWeatherResponse(JObject data)
        {
            var cityName = data["name"].ToString();
            var weatherDescription = data["weather"][0]["description"].ToString();
            var temperature = data["main"]["temp"].ToObject<double>();
            var humidity = data["main"]["humidity"].ToObject<int>();
            var windSpeed = data["wind"]["speed"].ToObject<double>();
            var feelsLike = data["main"]["feels_like"].ToObject<double>();

            string temperatureUnit = defaultUnit == "metric" ? "°C" : "°F";
            string windSpeedUnit = defaultUnit == "metric" ? "m/s" : "mph";
            string clothingRecommendation = GetClothingRecommendation(feelsLike, defaultUnit);

            return $"Weather in {cityName}:\n" +
                   $"Description: {weatherDescription}\n" +
                   $"Temperature: {temperature}{temperatureUnit}\n" +
                   $"Feels Like: {feelsLike}{temperatureUnit}\n" +
                   $"Humidity: {humidity}%\n" +
                   $"Wind Speed: {windSpeed} {windSpeedUnit}\n" +
                   $"Clothing Recommendation: {clothingRecommendation}";
        }

        private static string GetClothingRecommendation(double feelsLike, string unit)
        {
            double tempCelsius = unit == "metric" ? feelsLike : (feelsLike - 32) * 5 / 9;

            if (tempCelsius < 0)
            {
                return "Wear a heavy coat, gloves, and a hat.";
            }
            else if (tempCelsius < 10)
            {
                return "Wear a coat and a warm sweater.";
            }
            else if (tempCelsius < 20)
            {
                return "Wear a light jacket or sweater.";
            }
            else if (tempCelsius < 30)
            {
                return "Wear a t-shirt and shorts.";
            }
            else
            {
                return "Wear light, breathable clothing.";
            }
        }

        private static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            string errorMessage;
            if (exception is ApiRequestException apiRequestException)
            {
                errorMessage = $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}";
            }
            else
            {
                errorMessage = exception.ToString();
            }

            Console.WriteLine(errorMessage);
            return Task.CompletedTask;
        }
    }
}
