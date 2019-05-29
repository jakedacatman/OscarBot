using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace OscarBot.Classes
{
    public class GuildQueue
    {
        [Key]
        public ulong GuildId { get; set; }
        public ICollection<Song> Queue { get; set; }
        public ICollection<Skip> Skipped { get; set; }
    }

    public class Skip
    {
        public int Id { get; set; }
        public ulong UserId { get; set; }
        public string SongUrl { get; set; }
    }
}
