using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Discord.Addons.Interactive;
using System.Reflection;
using System.IO;
using Victoria;
using OscarBot.Classes;
using OscarBot.Services;
using OscarBot.TypeReaders;

namespace OscarBot
{
    class Program
    {
        private DiscordShardedClient _client;
        private CommandService _commands;
        private IServiceProvider _services;
        private LavaShardClient _manager;
        private LavaRestClient _lavaRestClient;
        private readonly Configuration lavaConfig = new Configuration { AutoDisconnect = true, InactivityTimeout = TimeSpan.FromSeconds(30), PreservePlayers = true, LogSeverity = LogSeverity.Verbose, ReconnectAttempts = 20, ReconnectInterval = TimeSpan.FromSeconds(3) };

        public static void Main(string[] args) => new Program().Start().GetAwaiter().GetResult();

        public async Task Start()
        {
            _client = new DiscordShardedClient(new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Verbose,
                AlwaysDownloadUsers = false,
                ConnectionTimeout = int.MaxValue,
                TotalShards = 4,
                DefaultRetryMode = RetryMode.AlwaysRetry,
                MessageCacheSize = 1024,
                ExclusiveBulkDelete = true
            });

            _commands = new CommandService(new CommandServiceConfig
            {
                ThrowOnError = true,
                CaseSensitiveCommands = false,
                LogLevel = LogSeverity.Verbose,
                DefaultRunMode = RunMode.Async,
                IgnoreExtraArgs = false
            });

            _manager = new LavaShardClient();

            _lavaRestClient = new LavaRestClient(lavaConfig);

            _services = new ServiceCollection()
                .AddSingleton(_client)
                .AddSingleton(_commands)
                .AddSingleton(_manager)
                .AddSingleton(_lavaRestClient)
                .AddSingleton<DbService>()
                .AddSingleton<ModerationService>()
                .AddSingleton<MiscService>()
                .AddSingleton<InteractiveService>()
                .AddSingleton<ImageService>()
                .AddSingleton<MusicService>()
                .AddDbContext<EntityContext>()
                .BuildServiceProvider();

            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

            _commands.AddTypeReader<TimeSpan>(new TimeSpanReader());

            await _client.LoginAsync(TokenType.Bot, _services.GetService<DbService>().GetApiKey("discord"));
            await _client.StartAsync();

            await _client.SetActivityAsync(new Game($"myself start up {_client.Shards.Count} shards", ActivityType.Watching));

            _client.Log += Log;
            _client.MessageReceived += MsgReceived;
            _client.MessageUpdated += MsgUpdated;

            int counter = 1;
            _client.ShardConnected += async (DiscordSocketClient client) =>
            {
                if (counter >= _client.Shards.Count)
                {
                    await _manager.StartAsync(_client, lavaConfig);
                    await _client.SetActivityAsync(new Game($"over {counter} out of {_client.Shards.Count} shards", ActivityType.Watching));
                }   
                counter++;
            };


            _manager.Log += Log;
            _commands.Log += Log;

            await Task.Delay(-1);
        }

        private Task Log(LogMessage msg)
        {
            try
            {
                var toWrite = $"{DateTime.Now,19} [{msg.Severity,8}] {msg.Source}: {msg.Message ?? "no message"}";
                if (msg.Exception != null) toWrite += $" (exception: {msg.Exception})";
                Console.WriteLine(toWrite);
                return Task.CompletedTask;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return Task.CompletedTask;
            }
        }

        private async Task MsgReceived(SocketMessage _msg)
        {
            try
            {
                if (!(_msg is SocketUserMessage msg) || _msg == null || string.IsNullOrEmpty(msg.Content)) return;
                ShardedCommandContext context = new ShardedCommandContext(_client, msg);

                string prefix = await _services.GetService<DbService>().GetPrefixAsync(context.Guild.Id);

                int argPos = prefix.Length - 1;
                if (!msg.HasStringPrefix(prefix, ref argPos)) return;

                if (context.User.IsBot) return;
                var result = await _commands.ExecuteAsync(context, argPos, _services);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
        private async Task MsgUpdated(Cacheable<IMessage, ulong> oldMsg, SocketMessage _msg, ISocketMessageChannel channel)
        {
            try
            {
                if (!(_msg is SocketUserMessage msg) || string.IsNullOrEmpty(msg.Content)) return;

                ShardedCommandContext context = new ShardedCommandContext(_client, msg);

                var old = await oldMsg.GetOrDownloadAsync();

                if (old == null) return;

                if (old.EditedTimestamp.HasValue || !msg.EditedTimestamp.HasValue) return;
                if (old.Timestamp.UtcDateTime.AddMinutes(1d) < msg.EditedTimestamp.Value.UtcDateTime) return;

                string prefix = await _services.GetService<DbService>().GetPrefixAsync(context.Guild.Id);

                int argPos = prefix.Length - 1;
                if (!msg.HasStringPrefix(prefix, ref argPos)) return;

                if (context.User.IsBot) return;
                await _commands.ExecuteAsync(context, argPos, _services);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }

}