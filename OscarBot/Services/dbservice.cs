using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OscarBot.Classes;
using Discord.WebSocket;
using System.IO;
using Microsoft.Extensions.DependencyInjection;

namespace OscarBot.Services
{
    public class DbService
    {
        private readonly IServiceProvider _services;
        private const string defaultPrefix = "osc.";

        public DbService(IServiceProvider services)
        {
            _services = services;
        }

        public async Task<int> AddApiKeyAsync(string service, string key)
        {
            using (var scope = _services.CreateScope())
            {
                var _db = scope.ServiceProvider.GetRequiredService<EntityContext>();

                _db.ApiKeys.Add(new ApiKey { Service = service, Key = key });
                return await _db.SaveChangesAsync();
            }
        }
        public string GetApiKey(string service)
        {
            using (var scope = _services.CreateScope())
            {
                var _db = scope.ServiceProvider.GetRequiredService<EntityContext>();

                var query = _db.ApiKeys.Where(x => x.Service == service);
                return query.Count() > 0 ? query.Single().Key : null;
            }
        }

        public async Task<string> GetPrefixAsync(ulong guildId)
        {
            using (var scope = _services.CreateScope())
            {
                var _db = scope.ServiceProvider.GetRequiredService<EntityContext>();

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
        }
    }
}
