using System.Collections.Generic;

namespace OscarBot.Classes
{
    public class GuildQueue
    {
        public GuildQueue(ulong guildId, ulong channelId)
        {
            GuildId = guildId;
            ChannelId = channelId;
        }

        public ulong GuildId { get; }
        public ulong ChannelId { get; }
        public Queue<Song> Queue { get; set; } = new Queue<Song>();
        public List<Skip> Skipped { get; set; } = new List<Skip>();
    }

    public class Skip
    {
        public int Id { get; set; }
        public ulong UserId { get; set; }
        public string SongUrl { get; set; }
    }
}
