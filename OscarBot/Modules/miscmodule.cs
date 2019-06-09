using System;
using System.Collections.Generic;   
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Discord.Commands;
using OscarBot.Services;
using Victoria;
using Discord.Addons.Interactive;

namespace OscarBot.Modules
{
    [Name("Miscellaneous")]
    public class MiscModule : InteractiveBase<ShardedCommandContext>
    {
        private readonly DiscordShardedClient _client;
        private readonly MiscService _misc;
        private readonly DbService _db;

        public MiscModule(DiscordShardedClient client, MiscService misc, DbService db)
        {
            _client = client;
            _misc = misc;
            _db = db;
        }

        [Command("eval")]
        [Alias("evaluate")]
        [Summary("Evaluates C# code.")]
        [RequireOwner]
        public async Task EvalCmd([Summary("The code to evaluate."), Remainder] string code)
        {
            try
            {
                await ReplyAsync(embed: (await _misc.EvaluateAsync(Context, code)).Build());
            }
            catch (System.Net.WebException ex ) when (ex.Message == "The remote server returned an error: (400) Bad Request.")
            {
                await ReplyAsync("Bisoga returned an HTTP 400 error (bad request). Are you doing something shady? :thinking:");
            }
            catch (Exception e)
            {
                await ReplyAsync(embed: _misc.GenerateErrorMessage(e).Build());
            }
        }
    }
}
