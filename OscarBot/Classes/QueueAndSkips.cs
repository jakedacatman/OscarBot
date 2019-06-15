using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OscarBot.Classes
{
    public class QueueAndSkips
    {
        public Queue<Song> Queue { get; set; } = new Queue<Song>();
        public List<Skip> Skips { get; set; } = new List<Skip>();
        public ulong GuildId { get; set; }
        public ulong ChannelId { get; set; }

        public QueueAndSkips(ulong guildId, ulong channelId)
        {
            GuildId = guildId;
            ChannelId = channelId;
        }
    }
}
