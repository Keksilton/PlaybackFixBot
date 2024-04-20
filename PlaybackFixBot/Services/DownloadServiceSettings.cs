namespace PlaybackFixBot.Services;

public sealed class DownloadServiceSettings
{
    public const string SectionName = "DownloadServiceSettings";
    public long FileSizeLimit { get; set; } = 50 * 1024 * 1024;
}