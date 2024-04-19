using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImageMagick;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Events;

namespace PlaybackFixBot.Services
{
    public class EncodeService
    {

        public EncodeService()
        {

        }

        public async Task ConvertToDiscordPlayableAsync(string origFileName, string targetName, ConversionProgressEventHandler handler)
        {
            var info = await FFmpeg.GetMediaInfo(origFileName);
            var video = info.VideoStreams.FirstOrDefault()?.SetCodec(VideoCodec.libx264); // hardware encoding for Raspberry Pi 4 "h264_v4l2m2m", but not supported by discord
            var audio = info.AudioStreams.FirstOrDefault()?.SetCodec(AudioCodec.aac);
            var conversion = FFmpeg.Conversions.New().AddStream<IStream>(video, audio).SetOutput(targetName);
            conversion.OnProgress += handler;
            _ = await conversion.Start();
        }

        public async Task<Stream> ConvertToPng(Stream stream)
        {
            using var image = new MagickImage(stream);
            var ms = new MemoryStream();
            image.Format = MagickFormat.Png;
            await image.WriteAsync(ms);
            return ms;
        }
    }
}
