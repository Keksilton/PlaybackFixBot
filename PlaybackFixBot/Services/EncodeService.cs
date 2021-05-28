using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            var video = info.VideoStreams.FirstOrDefault()?.SetCodec(VideoCodec.libx264);
            var audio = info.AudioStreams.FirstOrDefault()?.SetCodec(AudioCodec.aac);
            var conversion = FFmpeg.Conversions.New().AddStream<IStream>(video, audio).SetOutput(targetName);
            conversion.OnProgress += handler;
            var result = await conversion.Start();
        }
    }
}
