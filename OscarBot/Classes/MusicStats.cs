using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OscarBot.Classes
{
    public class MusicStats
    {
        public long MemoryUsage { get; }
        public int PlayingPlayers { get; }
        public int SongsQueued { get; }

        public MusicStats(long usage, int playing, int songs)
        {
            MemoryUsage = usage;
            PlayingPlayers = playing;
            SongsQueued = songs;
        }
    }
}
