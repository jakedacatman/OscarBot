using System;
using System.Collections;
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

    public class ModerationActionCollection : IEnumerable<ModerationAction>, IEnumerable
    {
        [Key]
        public ulong GuildId { get; set; }
        public List<ModerationAction> Actions { get; set; } = new List<ModerationAction>();
        public IEnumerator<ModerationAction> GetEnumerator() => Actions.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => Actions.GetEnumerator();

        public void Add(ModerationAction m) => Actions.Add(m);
        public bool Remove(ModerationAction m) => Actions.Remove(m);
        public int RemoveAll(Predicate<ModerationAction> pred) => Actions.RemoveAll(pred);
    }
}
