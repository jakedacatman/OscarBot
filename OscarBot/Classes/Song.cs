using System;

namespace OscarBot.Classes
{
    public class Song
    {
        public string URL { get; set; }
        public string AudioURL { get; set; }
        public ulong QueuerId { get; set; }
        public string Name { get; set; }
        public ulong GuildId { get; set; }
        public ulong ChannelId { get; set; }
        public string Length { get; set; }
        public string Author { get; set; }
        public string Thumbnail { get; set; }
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    }
}