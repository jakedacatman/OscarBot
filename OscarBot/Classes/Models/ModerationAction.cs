using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OscarBot.Classes
{
    public class ModerationAction
    {
        public ActionType Type { get; set; }
        public ulong GuildId { get; set; }
        [Key]
        public ulong UserId { get; set; }
        public ulong ModeratorId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public DateTime ReverseAfter { get; set; }
        public string Reason { get; set; } = null;

        public enum ActionType
        {
            Mute = 1,
            Block = 2,
            Kick = 3,
            Ban = 4,
        }
    }

    public class ModerationActionCollection
    {
        [Key]
        public ulong GuildId { get; set; }
        public List<ModerationAction> Actions { get; set; }
    }
}
