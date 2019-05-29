using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OscarBot.Classes;
using Discord.WebSocket;
using System.IO;

namespace OscarBot.Services
{
    public class DbService
    {
        public static readonly EntityContext _db = new EntityContext();
        private const string defaultPrefix = "osc.";
        private ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new ConcurrentDictionary<string, SemaphoreSlim>();

        public async Task<int> AddApiKeyAsync(string service, string key)
        {
            _db.ApiKeys.Add(new ApiKey { Service = service, Key = key });
            return await _db.SaveChangesAsync();
        }
        public string GetApiKey(string service)
        {
            var query = _db.ApiKeys.Where(x => x.Service == service);
            return query.Count() > 0 ? query.Single().Key : null;
        }

        public async Task<string> GetPrefixAsync(ulong guildId)
        {
            var semaphore = _semaphores.GetOrAdd("GetPrefixAsync", new SemaphoreSlim(1, 1));

            await semaphore.WaitAsync();
            try
            {
                var c = _db.Prefixes;
                var query = c.Where(x => x.Id == guildId);
                Prefix pref;
                if (query.Count() == 0)
                {
                    pref = new Prefix { GuildPrefix = defaultPrefix, Id = guildId };
                    c.Add(pref);
                    await _db.SaveChangesAsync();
                }
                else
                    pref = query.Single();
                return pref.GuildPrefix;
            }
            finally
            {
                semaphore.Release();
            }
        }
    }
}
