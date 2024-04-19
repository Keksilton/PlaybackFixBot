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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog.Events;

namespace PlaybackFixBot
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            Console.Title = "Playback fix bot";

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message} {NewLine}{Exception}")
                .WriteTo.File("logs/PlaybackFixBot-.log",
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message} {NewLine}{Exception}",
                    rollingInterval: RollingInterval.Day)
                .Enrich.FromLogContext()
                .CreateBootstrapLogger();

            var host = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings()
            {
                Args = args,
                ApplicationName = "PlaybackFixBot",
                DisableDefaults = true
            });

            host.Configuration.AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables();

            host.Services.AddSerilog(c => c.MinimumLevel.Debug()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message} {NewLine}{Exception}")
                .WriteTo.File("logs/PlaybackFixBot-.log",
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message} {NewLine}{Exception}",
                    rollingInterval: RollingInterval.Day)
                .Enrich.FromLogContext());

            host.Services.AddOptions<DownloadServiceSettings>().BindConfiguration(DownloadServiceSettings.SectionName)
                .Services.AddOptions<AppSettings>().BindConfiguration(string.Empty);

            host.Services.AddSingleton<DiscordSocketClient>()
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
                .AddSingleton<DownloadService>();

            var app = host.Build();
            // Set up logger
            Log.Debug("Starting");

            //Client startup
            var settings = app.Services.GetRequiredService<IOptions<AppSettings>>();
            await using var client = app.Services.GetRequiredService<DiscordSocketClient>();
            client.Log += LogAsync;
            app.Services.GetRequiredService<CommandService>().Log += LogAsync;
            // Tokens should be considered secret data and never hard-coded.
            await client.LoginAsync(TokenType.Bot, settings.Value.Token);
            await client.StartAsync();

            // Here we initialize the logic required to register our commands.
            await app.Services.GetRequiredService<CommandHandlingService>().InitializeAsync();

            await Task.Delay(Timeout.Infinite);
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
    }
}