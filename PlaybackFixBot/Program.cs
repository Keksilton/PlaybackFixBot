using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PlaybackFixBot.Services;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.IO;
using Serilog.Events;

namespace PlaybackFixBot
{
    class Program
    {

        public const string TOKEN_PLACEHOLDER = "REPLACE_WITH_TOKEN";
        public static async Task Main(string[] args)
        {
            Console.Title = "Playback fix bot";
            
            // Set up logger
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message} {NewLine}{Exception}")
                .WriteTo.File("logs/PlaybackFixBot-.log", outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message} {NewLine}{Exception}", rollingInterval: RollingInterval.Day)
                .Enrich.FromLogContext()
                .CreateLogger();
            Log.Debug("Starting");

            // Set up config
            var configInfo = new FileInfo("appsettings.json");
            var config = new ConfigurationBuilder()
                .AddJsonFile(configInfo.Name, true)
                .AddEnvironmentVariables()
                .Build();
            var settings = config.Get<AppSettings>();
            if(settings== null || string.IsNullOrEmpty(settings.Token) || settings.Token == TOKEN_PLACEHOLDER)
            {
                settings = new AppSettings()
                {
                    Token = TOKEN_PLACEHOLDER
                };
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(settings, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(configInfo.Name, json);
                Log.Error($"No token present in {configInfo.Name}");
                return;
            }
            //Client startup
            using (var services = ConfigureServices())
            {
                using (var client = services.GetRequiredService<DiscordSocketClient>())
                {
                    client.Log += LogAsync;
                    services.GetRequiredService<CommandService>().Log += LogAsync;
                    // Tokens should be considered secret data and never hard-coded.
                    await client.LoginAsync(TokenType.Bot, settings.Token);
                    await client.StartAsync();

                    // Here we initialize the logic required to register our commands.
                    await services.GetRequiredService<CommandHandlingService>().InitializeAsync();
                    await Task.Delay(Timeout.Infinite);
                }
            }
        }

        private static async Task LogAsync(LogMessage message)
        {
            var severity = message.Severity switch
            {
                LogSeverity.Critical => LogEventLevel.Fatal,
                LogSeverity.Error => LogEventLevel.Error,
                LogSeverity.Warning => LogEventLevel.Warning,
                LogSeverity.Info => LogEventLevel.Information,
                LogSeverity.Verbose => LogEventLevel.Verbose,
                LogSeverity.Debug => LogEventLevel.Debug,
                _ => LogEventLevel.Information
            };
            Log.Write(severity, message.Exception, "[{Source}] {Message}", message.Source, message.Message);
            await Task.CompletedTask;
        }

        private static ServiceProvider ConfigureServices()
        {
            return new ServiceCollection()
                .AddSingleton<DiscordSocketClient>()
                .AddSingleton<DiscordSocketConfig>((sp) =>
                {
                    return new DiscordSocketConfig()
                    {
                        GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
                    };
                })
                .AddSingleton<CommandService>()
                .AddSingleton<CommandHandlingService>()
                .AddSingleton<HttpClient>()
                .AddSingleton<System.Net.WebClient>()
                .AddSingleton<EncodeService>()
                .AddSingleton<DownloadService>()
                .BuildServiceProvider();
        }
    }
}
