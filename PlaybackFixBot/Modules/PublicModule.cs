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
    public class PublicModule : ModuleBase<SocketCommandContext>
    {
        // Dependency Injection will fill this value in for us
        public DownloadService DownloadService { get; set; }
        public EncodeService EncodeService { get; set; }

        [Command("ping")]
        [Alias("pong", "hello")]
        [RequireOwner]
        public Task PingAsync()
        {
            Log.Debug("Command: ping");
            return ReplyAsync("pong!");
        }


        // Setting a custom ErrorMessage property will help clarify the precondition error
        [Command("fix")]
        [RequireContext(ContextType.Guild, ErrorMessage = "Sorry, this command must be ran from within a server, not a DM!")]
        public async Task FixPlayback()
        {
            Log.Debug("Command: fix");
            var refMessage = Context.Message.ReferencedMessage;
            foreach (var attachment in refMessage.Attachments)
            {
                if (attachment.Filename.EndsWith(".mp4") || attachment.Filename.EndsWith(".webm") || attachment.Filename.EndsWith(".m4a") || attachment.Filename.EndsWith(".mov") || attachment.Filename.EndsWith("avi"))
                {
                    //var mediaMatch = Regex.Match(attachment.Url, @"^https?:\/\/media.discordapp.com\/");
                    var cdnMatch = Regex.Match(attachment.Url, @"^https?:\/\/cdn.discordapp.com\/");
                    if (cdnMatch.Success)
                    {
                        Log.Debug($"Fixing {attachment.Filename}");
                        var statusMessage = await ReplyAsync("Downloading");
                        Log.Debug($"Downloading {attachment.Filename}");
                        var ext = new FileInfo(attachment.Filename).Extension;
                        var oname = await DownloadService.DownloadAsync(attachment.Url);

                        if (string.IsNullOrEmpty(oname))
                        {
                            Log.Debug($"File too big {attachment.Filename}");
                            await statusMessage.ModifyAsync((message) => { message.Content = new Optional<string>("The download is too powerful :c"); });
                            continue;
                        }

                        File.Move(oname, oname + ext);
                        oname += ext;
                        Log.Debug($"Converting {attachment.Filename}");
                        var fname = Path.Combine(DownloadService.CACHE_PATH, attachment.Filename.EndsWith(".mp4") ? attachment.Filename : (attachment.Filename + ".mp4"));
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        void handler(object sender, ConversionProgressEventArgs args)
                        {
                            if(sw.ElapsedMilliseconds > 1000)
                            {
                                sw.Reset();
                                try
                                {
                                    statusMessage.ModifyAsync((message) => { message.Content = new Optional<string>($"Processing: {args.Percent}%"); }).GetAwaiter().GetResult();
                                }
                                catch(Exception ex)
                                {
                                    Log.Error(ex, "An error occured updating status message");
                                }
                                sw.Start();
                            }
                        }
                        await EncodeService.ConvertToDiscordPlayableAsync(oname, fname, handler);
                        var info = new FileInfo(fname);
                        if (info.Length > 50 * 1024 * 1024)
                        {
                            await statusMessage.ModifyAsync((message) => { message.Content = new Optional<string>("The upload is too powerful :c"); });
                            continue;
                        }
                        Log.Debug($"Uploading {attachment.Filename}");
                        try
                        {
                            var reply = await Context.Message.Channel.SendFileAsync(fname);
                        }
                        catch(Exception ex)
                        {
                            Log.Error(ex, "An error occured uploading file");
                        }
                        await statusMessage.DeleteAsync();
                        Log.Debug($"Deleteing stuff {attachment.Filename}");
                        File.Delete(oname);
                        File.Delete(fname);
                        Log.Debug($"Fixed {attachment.Filename}");
                    }
                }
            }

        }
    }
}
