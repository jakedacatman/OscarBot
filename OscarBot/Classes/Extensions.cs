using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using SharpLink;
using OscarBot.Services;

namespace OscarBot.Classes
{

    public static class IEnumerableExtensions
    {
        public static string ToString<T>(this IEnumerable<T> t, string separator)
        {
            return string.Join(separator, t);
        }
    }

    public static class IUserExtensions
    {
        public static bool IsQueuer(this IUser user, Song s)
        {
            return user.Id == s.QueuerId;
        }
    }

    public static class StringExtensions
    {
        public static bool WillExit(this string s, out string message)
        {
            if (s.Contains("Environment.Exit"))
            {
                message = "This code calls Environment.Exit.";
                return true;
            }
            message = "This code will not exit.";
            return false;
        }
    }

    public static class ICollectionExcensions
    {
        public static async Task<int> QueueUp(this ICollection<Song> queue, ulong guildId, Song s)
        {
            queue.Add(s);
            var queues = DbService._db.Queues;
            var query = queues.Where(x => x.GuildId == guildId);
            if (query.Count() == 0)
                queues.Add(new GuildQueue { Queue = queue, GuildId = guildId, Skipped = new List<Skip>() });
            else
                query.Single().Queue = queue;
            return await DbService._db.SaveChangesAsync();
        }
        public static async Task<Song> Pop(this ICollection<Song> queue, ulong guildId)
        {
            Song s = queue.First();
            queue.Remove(s);
            var queues = DbService._db.Queues;
            var query = queues.Where(x => x.GuildId == guildId);
            if (query.Count() == 0)
                queues.Add(new GuildQueue { Queue = queue, GuildId = guildId, Skipped = new List<Skip>() });
            else
                query.Single().Queue = queue;
            await DbService._db.SaveChangesAsync();
            return s;
        }
    }

    public static class ListExtensions
    {
        public static async Task<int> AddUser(this List<Skip> list, Skip s, ulong guildId)
        {
            list.Add(s);
            var queues = DbService._db.Queues;
            var query = queues.Where(x => x.GuildId == guildId);
            if (query.Count() == 0)
                queues.Add(new GuildQueue { Queue = new List<Song>(), GuildId = guildId, Skipped = list});
            else
                query.Single().Skipped = list;
            return await DbService._db.SaveChangesAsync();
        }
        public static async Task<int> RemoveUsers(this List<Skip> list, ulong guildId)
        {
            list.RemoveAll(x => x.UserId >= 0);
            var queues = DbService._db.Queues;
            var query = queues.Where(x => x.GuildId == guildId);
            if (query.Count() == 0)
                queues.Add(new GuildQueue { Queue = new List<Song>(), GuildId = guildId, Skipped = list });
            else
                query.Single().Skipped = list;
            return await DbService._db.SaveChangesAsync();
        }
    }
}
