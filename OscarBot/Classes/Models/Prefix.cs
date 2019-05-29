using System.ComponentModel.DataAnnotations;

namespace OscarBot.Classes
{
    public class Prefix
    {
        [Key]
        public ulong Id { get; set; }
        public string GuildPrefix { get; set; }
    }
}
