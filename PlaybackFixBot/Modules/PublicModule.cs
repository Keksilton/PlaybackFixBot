using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Serilog;
using Xabe.FFmpeg.Events;
using PlaybackFixBot.Services;
using Microsoft.Extensions.Logging;

namespace PlaybackFixBot.Modules
{
    public partial class PublicModule : ModuleBase<SocketCommandContext>
    {
        // Dependency Injection will fill this value in for us
        public DownloadService DownloadService { get; set; }
        public ILogger<PublicModule> Logger { get; set; }
        public EncodeService EncodeService { get; set; }

        [Command("ping")]
        [Alias("pong", "hello")]
        [RequireOwner]
        public Task PingAsync()
        {
            Logger.LogDebug("Command: ping");
            return ReplyAsync("pong!");
        }


        // Setting a custom ErrorMessage property will help clarify the precondition error
        [Command("fix")]
        [RequireContext(ContextType.Guild,
            ErrorMessage = "Sorry, this command must be ran from within a server, not a DM!")]
        public async Task FixPlayback()
        {
            Logger.LogDebug("Command: fix");
            var refMessage = Context.Message.ReferencedMessage;
            foreach (var attachment in refMessage.Attachments)
            {
                try
                {
                    if (IsValidVideo(attachment))
                        await FixVideo(attachment);
                    if (IsValidImage(attachment))
                        await FixImage(attachment);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "An exception occurred while processing attachment");
                }
            }
        }

        private async Task FixImage(IAttachment attachment)
        {
            Logger.LogDebug("Fixing {FileName}", attachment.Filename);
            var statusMessage = await ReplyAsync("Processing");
            Logger.LogDebug("Downloading {FileName}", attachment.Filename);
            await using var webpStream = await DownloadService.GetStreamAsync(attachment.Url);
            Logger.LogDebug("Converting {FileName}", attachment.Filename);
            var pngFileName = attachment.Filename[..attachment.Filename.LastIndexOf('.')] + ".png";
            await using var pngStream = await EncodeService.ConvertToPng(webpStream);
            try
            {
                var reply = await Context.Message.Channel.SendFileAsync(pngStream, pngFileName);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "An error occured uploading file");
            }
        }

        private async Task FixVideo(IAttachment attachment)
        {
            Logger.LogDebug("Fixing {FileName}", attachment.Filename);
            var statusMessage = await ReplyAsync("Downloading");
            Logger.LogDebug("Downloading {FileName}", attachment.Filename);
            var ext = new FileInfo(attachment.Filename).Extension;
            var oname = await DownloadService.DownloadAsync(attachment.Url);

            if (string.IsNullOrEmpty(oname))
            {
                Logger.LogDebug("File too big {FileName}", attachment.Filename);
                await statusMessage.ModifyAsync((message) =>
                {
                    message.Content = new Optional<string>("The download is too powerful :c");
                });
                return;
            }

            var destinationFileName = oname + ext;
            File.Move(oname, destinationFileName);
            oname = destinationFileName;
            Logger.LogDebug("Converting {FileName}", attachment.Filename);
            var fileName = Path.Combine(DownloadService.CachePath,
                attachment.Filename.EndsWith(".mp4") ? attachment.Filename : (attachment.Filename + ".mp4"));
            var sw = System.Diagnostics.Stopwatch.StartNew();

            void handler(object sender, ConversionProgressEventArgs args)
            {
                if (sw.ElapsedMilliseconds <= 1000) return;

                sw.Reset();
                try
                {
                    statusMessage.ModifyAsync((message) =>
                    {
                        message.Content = new Optional<string>($"Processing: {args.Percent}%");
                    }).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "An error occured updating status message");
                }

                sw.Start();
            }

            await EncodeService.ConvertToDiscordPlayableAsync(oname, fileName, handler);
            var info = new FileInfo(fileName);
            if (info.Length > 50 * 1024 * 1024)
            {
                await statusMessage.ModifyAsync((message) =>
                {
                    message.Content = new Optional<string>("The upload is too powerful :c");
                });
                return;
            }

            Logger.LogDebug("Uploading {FileName}", attachment.Filename);
            try
            {
                var reply = await Context.Message.Channel.SendFileAsync(fileName);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occured uploading file");
            }

            await statusMessage.DeleteAsync();
            Logger.LogDebug("Deleting stuff {FileName}", attachment.Filename);
            File.Delete(oname);
            File.Delete(fileName);
            Logger.LogDebug("Fixed {FileName}", attachment.Filename);
        }

        private static readonly Regex _cdnRegex = CdnRegex();

        private static bool IsValidVideo(IAttachment attachment)
        {
            if (!attachment.Filename.EndsWith(".mp4") && !attachment.Filename.EndsWith(".webm") &&
                !attachment.Filename.EndsWith(".m4a") && !attachment.Filename.EndsWith(".mov") &&
                !attachment.Filename.EndsWith("avi")) return false;

            //var mediaMatch = Regex.Match(attachment.Url, @"^https?:\/\/media.discordapp.com\/");
            var cdnMatch = _cdnRegex.Match(attachment.Url);
            if (!cdnMatch.Success) return false;

            return true;
        }

        private static bool IsValidImage(IAttachment attachment)
        {
            if (!attachment.Filename.EndsWith(".webp")) return false;

            //var mediaMatch = Regex.Match(attachment.Url, @"^https?:\/\/media.discordapp.com\/");
            var cdnMatch = _cdnRegex.Match(attachment.Url);
            if (!cdnMatch.Success) return false;

            return true;
        }

        [GeneratedRegex(@"^https?:\/\/cdn.discordapp.com\/", RegexOptions.Compiled)]
        private static partial Regex CdnRegex();
    }
}