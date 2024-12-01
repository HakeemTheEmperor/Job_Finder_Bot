using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DotNetEnv;
using Job_Bot.Models;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Job_Bot
{
    internal class Program
    {
        private static TelegramBotClient bot;
        private static Dictionary<long, UserConfig> _userconfigurations = new Dictionary<long, UserConfig>();
        private static readonly HttpClient client = new HttpClient();

        static async Task Main(string[] args)
        {
            Env.Load();
            string token = Env.GetString("BOT_TOKEN");
            Console.WriteLine(token);
            bot = new TelegramBotClient(token);

            var cts = new CancellationTokenSource();
            var cancellationToken = cts.Token;
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>()
            };

            bot.StartReceiving(HandleUpdateAsync, HandleErrorAsync, receiverOptions, cancellationToken);
            Console.WriteLine("Bot is running. Press any key to exit...");
            Console.ReadKey();

            cts.Cancel();
        }

        private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update,
            CancellationToken cancellationToken)
        {
            if (update.Message != null && update.Message.Text?.Trim() == "/start")
            {
                await SendMainMenu(botClient, update.Message.Chat.Id);
            }            
            else if (update.Message != null && update.Message.Text?.Trim() == "/fetch")
            {
                await FetchJobs(botClient, update.Message.Chat.Id);
            }
            else if (update.CallbackQuery != null)
            {
                await HandleCallbackQueryAsync(botClient, update.CallbackQuery);
            }
            else if (update.Message != null)
            {
                await HandleMessageAsync(botClient, update.Message);
            }
        }

        private static async Task FetchJobs(ITelegramBotClient bot, long chatId)
        {
            if (!_userconfigurations.ContainsKey(chatId))
            {
                _userconfigurations[chatId] = new UserConfig();
            }

            var userConfig = _userconfigurations[chatId];
            userConfig.CurrentStep = "fetchJobs";
            await bot.SendMessage(chatId, "What type of jobs are you looking for?");
        }

        private static async Task<Task> HandleErrorAsync(ITelegramBotClient botClient, Exception exception,
            CancellationToken cancellationToken)
        {
            Console.WriteLine($"Error: {exception.Message}");
            return Task.CompletedTask;
        }

        private static async Task SendMainMenu(ITelegramBotClient bot, long chatId)
        {
            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Create Account for job postings", "configure"),
                    InlineKeyboardButton.WithCallbackData("Fetch Jobs", "fetch"),
                }
            });

            await bot.SendMessage(chatId,
                "Welcome to JobFinder! Your site for best job updates. What would you like to do?",
                replyMarkup: inlineKeyboard);
        }

        private static async Task HandleCallbackQueryAsync(ITelegramBotClient bot, CallbackQuery callbackQuery)
        {
            long chatId = callbackQuery.Message.Chat.Id;
            string data = callbackQuery.Data;

            if (data == "configure")
            {
                if (!_userconfigurations.ContainsKey(chatId))
                    _userconfigurations[chatId] = new UserConfig();

                _userconfigurations[chatId].CurrentStep = "name";
                await bot.SendMessage(chatId, "Let's configure your profile. Please enter your name: ");
            }
            else if (data == "fetch")
            {
                await FetchJobs(bot, chatId);
            }
            else if (data == "main")
            {
                await SendMainMenu(bot, chatId);
            }
            else if (data == "exit")
            {
                _userconfigurations.Remove(chatId);
                await bot.SendMessage(chatId, "Thanks for using Job Finder");
            }
        }

        private static async Task WhatNext(ITelegramBotClient bot, long chatId)
        {
            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Main Menu", "main"),
                    InlineKeyboardButton.WithCallbackData("Exit", "exit"),
                }
            });

            await bot.SendMessage(chatId,
                "Would you like to do something else?",
                replyMarkup: inlineKeyboard);
        }

        private static async Task HandleMessageAsync(ITelegramBotClient bot, Message message)
        {
            long chatId = message.Chat.Id;

            if (!_userconfigurations.ContainsKey(chatId) || _userconfigurations[chatId].CurrentStep == null)
            {
                await bot.SendMessage(chatId, "Please use /start and choose 'Configure jobs' to begin");
                return;
            }
            
            var userConfig = _userconfigurations[chatId];
            string currentStep = userConfig.CurrentStep;

            switch (currentStep)
            {
                case "name":
                    if (!string.IsNullOrWhiteSpace(message.Text))
                    {
                        userConfig.Name = message.Text?.Trim();
                        userConfig.CurrentStep = "email";
                        await bot.SendMessage(chatId, "Great! Now, please enter your email: ");
                    }
                    else
                    {
                        await bot.SendMessage(chatId, "Please enter a valid name");
                    }
                    break;

                case "email":
                    if (!string.IsNullOrWhiteSpace(message.Text))
                    {
                        userConfig.Email = message.Text?.Trim();
                        userConfig.CurrentStep = "preference";
                        await bot.SendMessage(chatId, "Thanks! What are your job preferences? (i.e frontend engineer, backend developer etc.)");
                    }
                    else
                    {
                        await bot.SendMessage(chatId, "Please enter a valid email");
                    }
                    break;

                case "preference":
                    if (!string.IsNullOrWhiteSpace(message.Text))
                    {
                        userConfig.Preference = message.Text?.Trim();
                        userConfig.CurrentStep = "password";
                        await bot.SendMessage(chatId, "Almost done! Please enter your password:");
                    }
                    else
                    {
                        await bot.SendMessage(chatId, "Please enter a valid preference");

                    }
                    break;

                case "password":
                    if (!string.IsNullOrWhiteSpace(message.Text))
                    {
                        userConfig.Password = message.Text?.Trim();
                        userConfig.CurrentStep = "verifyOtp";
                        await CreateUser(bot, chatId, userConfig);
                    }
                    else
                    {
                        await bot.SendMessage(chatId, "Please enter a valid password");

                    }
                    break;

                case "verifyOtp":
                    if (!string.IsNullOrWhiteSpace(message.Text))
                    {
                        userConfig.Otp = message.Text?.Trim();
                        await VerifyUserAccount(bot, chatId, userConfig);
                        _userconfigurations.Remove(chatId);
                        await SendMainMenu(bot, chatId);
                    }
                    else
                    {
                        await bot.SendMessage(chatId, "Please enter a valid OTP");

                    }
                    break;

                case "fetchJobs":
                    string jobquery = message.Text?.Trim();
                    await FetchJobsFromApi(bot, chatId, jobquery);
                    userConfig.CurrentStep = null;
                    await WhatNext(bot, chatId);
                    break;

                default:
                    await bot.SendMessage(chatId, "Something went wrong. Please use /start to try again.");
                    break;
            }
        }

        private static async Task CreateUser(ITelegramBotClient bot, long chatId, UserConfig userConfig)
        {
            Env.Load();
            string baseUrl = Env.GetString("BASE_URL");
            string createUrl = $"{baseUrl}/users/create";
            string responseContent = "";
            var accountData = new
            {
                Name = userConfig.Name,
                Email = userConfig.Email,
                Preference = userConfig.Preference,
                Password = userConfig.Password
            };
            try
            {
                string jsonPayload = JsonConvert.SerializeObject(accountData);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(createUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    responseContent = await response.Content.ReadAsStringAsync();
                    var jsonObject = JsonConvert.DeserializeObject<JobFinderApiResponse>(responseContent);
                    responseContent = jsonObject?.Message ?? "User created successfully.";
                }
                else
                {
                    responseContent = $"Error: {response.StatusCode} - {response.ReasonPhrase}";
                }
            }
            catch (Exception e)
            {
                responseContent = $"Exception: {e.Message}";
            }
            await bot.SendMessage(chatId, responseContent);
        }

        private static async Task VerifyUserAccount(ITelegramBotClient bot, long chatId, UserConfig userConfig)
        {
            string verifyEndpoint = "https://localhost:7166/api/auth/verify_account";
            string responseContent = "";
            var verifyAccountData = new
            {
                Email = userConfig.Email,
                Otp = userConfig.Otp
            };

            try
            {
                string jsonPayload = JsonConvert.SerializeObject(verifyAccountData);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(verifyEndpoint, content);
                if (response.IsSuccessStatusCode)
                {
                    responseContent = await response.Content.ReadAsStringAsync();
                    var jsonObject = JsonConvert.DeserializeObject<JobFinderApiResponse>(responseContent);
                    responseContent = jsonObject?.Message ?? "User created successfully.";

                }
                else
                {
                    responseContent = $"Error: {response.StatusCode} - {response.ReasonPhrase}";
                }
            }
            catch (Exception e)
            {
                responseContent = $"Exception: {e.Message}";
            }

            await bot.SendMessage(chatId, responseContent);
        }

        private static async Task FetchJobsFromApi(ITelegramBotClient bot, long chatId, string query)
        {
            Env.Load();
            string baseUrl = Env.GetString("BASE_URL");
            string jobsUrl = $"{baseUrl}/jobs/search?query={query.Trim()}";
            string responseContent = "";

            try
            {
                HttpResponseMessage response = await client.GetAsync(jobsUrl);

                if (response.IsSuccessStatusCode)
                {
                    responseContent = await response.Content.ReadAsStringAsync();
                    var jobsResponse = JsonConvert.DeserializeObject<JobFinderApiResponseForJobs>(responseContent);
                    var jobs = jobsResponse.Jobs.Take(10).ToList();
                    if (jobs != null && jobs.Count > 0)
                    {
                        StringBuilder jobList = new StringBuilder("Here are some jobs matching your query:\n\n");

                        foreach (var job in jobs)
                        {
                            jobList.AppendLine($"**{job.JobTitle}** at {job.CompanyName}");
                            jobList.AppendLine($"Location: {job.Location}");
                            jobList.AppendLine($"Published on: {DateTime.Parse(job.DatePublished):yyyy MMMM dd}");
                            jobList.AppendLine($"Details: {job.JobUrl}");
                            jobList.AppendLine(new string('-', 40));
                        }
                        responseContent = jobList.ToString();
                    }
                    else
                    {
                        responseContent = "There were no great matches for your query.";
                    }
                }
            }
            catch (Exception e)
            {
                responseContent = $"Error fetching jobs: {e.Message}";
            }

            await bot.SendMessage(chatId, responseContent);
        }
    }
}
