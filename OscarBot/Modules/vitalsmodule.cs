using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Discord.Addons.Interactive;
using System.Diagnostics;
using OscarBot.Services;
using System.Net.Http;

namespace OscarBot.Modules
{
    [Name("Vitals")]
    public class VitalsModule : InteractiveBase<ShardedCommandContext>
    {
        private readonly DiscordShardedClient _client;
        private readonly CommandService _commands;
        private readonly MiscService _misc;

        private const int timeout = 10000; // 10 seconds

        public VitalsModule(DiscordShardedClient client, CommandService commands, MiscService misc)
        {
            _client = client;
            _commands = commands;
            _misc = misc;
        }

        [Command("ping")]
        [Summary("Pings the bot and returns the current shard's latency (or all if using the parameter -a).")]
        public async Task PingCommand([Summary("Use --all here in order to view the latency of all shards.")]string param = null)
        {
            try
            {
                var s = Stopwatch.StartNew();
                var m = await ReplyAsync("getting ping");
                s.Stop();
                var lat = s.ElapsedTicks;
                s.Restart();
                using (var h = new HttpClient())
                    await h.GetAsync("https://discordapp.com/api", new CancellationTokenSource(timeout).Token);
                s.Stop();



                string description;

                if (param == "-a" || param == "--all")
                {
                    List<string> latencies = new List<string>();

                    foreach (DiscordSocketClient shard in _client.Shards)
                    {
                        if (shard.ShardId == _client.GetShardIdFor(Context.Guild))
                            latencies.Add($"shard {shard.ShardId + 1}/{_client.Shards.Count} (current shard): **{shard.Latency} ms**");
                        else
                            latencies.Add($"shard {shard.ShardId + 1}/{_client.Shards.Count}: **{shard.Latency} ms**");
                    }

                    description = $"Latencies for all shards: \n{string.Join("\n", latencies)}\nMessage latency: **{lat / 10000d} ms**\nAPI latency: **{s.ElapsedTicks / 10000d} ms**";
                }
                else description = $"Latency for shard {_client.GetShardIdFor(Context.Guild) + 1}/{_client.Shards.Count}: **{_client.GetShardFor(Context.Guild).Latency} ms**\nMessage latency: **{lat / 10000d} ms**\nAPI latency: **{s.ElapsedTicks/10000d} ms**";
                EmbedBuilder embed = new EmbedBuilder()
                    .WithColor(Color.Orange)
                    .WithCurrentTimestamp()
                    .WithDescription(description);

                await m.ModifyAsync(x =>
                {
                    x.Content = "";
                    x.Embed = embed.Build();
                });
            }
            catch (Exception e)
            {
                await ReplyAsync(embed: _misc.GenerateErrorMessage(e).Build());
            }
        }

        [Command("commands")]
        [Alias("cmds")]
        [Summary("Sends a list of bot commands.")]
        public async Task CommandsCommand()
        {
            try
            {
                var fields = new List<EmbedFieldBuilder>();

                foreach (ModuleInfo module in _commands.Modules)
                {
                    var names = new List<string>();
                    foreach (CommandInfo cmd in module.Commands)
                    {
                        if (cmd.Summary == null) continue;
                        if (!names.Contains(cmd.Name)) names.Add(cmd.Name);
                    }
                    fields.Add(new EmbedFieldBuilder().WithIsInline(true).WithName(module.Name).WithValue($"**{string.Join(", ", names)}**"));
                }

                var embed = new EmbedBuilder()
                    .WithColor(_misc.RandomColor())
                    .WithTitle("Commands")
                    .WithFields(fields)
                    .WithCurrentTimestamp()
                    .WithThumbnailUrl(_client.CurrentUser.GetAvatarUrl(size: 512));

                await ReplyAsync(embed: embed.Build());

            }
            catch (Exception e)
            {
                await ReplyAsync(embed: _misc.GenerateErrorMessage(e).Build());
            }
        }

        [Command("help")]
        [Summary("Brings up information about a specific command.")]
        public async Task HelpCmd([Summary("The command to get information about."), Remainder] string command)
        {
            try
            {
                var cmds = _commands.Commands.Where(x => x.Name == command);
                if (cmds.Any())
                {
                    var firstCmd = cmds.First();

                    var fields = new List<EmbedFieldBuilder>
                    {
                        new EmbedFieldBuilder().WithName("Name").WithValue(firstCmd.Name).WithIsInline(true),
                        new EmbedFieldBuilder().WithName("Category").WithValue(firstCmd.Module.Name ?? "(none)").WithIsInline(true),
                        new EmbedFieldBuilder().WithName("Aliases").WithValue(firstCmd.Aliases.Count > 1 ? string.Join(", ", firstCmd.Aliases.Where(x => x != firstCmd.Name)) : "(none)").WithIsInline(true),
                        new EmbedFieldBuilder().WithName("Summary").WithValue(firstCmd.Summary ?? "(none)").WithIsInline(false),
                        new EmbedFieldBuilder().WithName("Parameters").WithValue(" ").WithIsInline(false)
                    };
                    int counter = 1;
                    StringBuilder sb = new StringBuilder();
                    foreach (var cmd in cmds)
                    {
                        var parameters = new List<string>();
                        foreach (ParameterInfo param in cmd.Parameters)
                        {
                            parameters.Add($"{param} ({param.Summary})");
                        }
                        
                        sb.Append($"**{counter}.**\n " + (parameters.Any() ? string.Join("\n", parameters) : "(none)") + "\n\n");
                        counter++;
                    }

                    var last = fields.Last();
                    fields.Remove(last);
                    last.WithValue(sb.ToString());
                    fields.Add(last);

                    EmbedBuilder embed = new EmbedBuilder()
                        .WithTitle($"Information for {firstCmd.Name}:")
                        .WithColor(_misc.RandomColor())
                        .WithCurrentTimestamp()
                        .WithFields(fields);

                    await ReplyAsync(embed: embed.Build());
                }
                else
                {
                    await ReplyAsync("This command does not exist.");
                    return;
                }
            }
            catch (Exception e)
            {
                await ReplyAsync(embed: _misc.GenerateErrorMessage(e).Build());
            }
        }
    }
}
