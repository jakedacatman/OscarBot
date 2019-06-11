using System;
using OscarBot.Services;
using Discord.Commands;
using Discord.WebSocket;
using Victoria;

namespace OscarBot.Classes
{
    public class Globals
    {
        public ShardedCommandContext Context { get; internal set; }
        public DiscordShardedClient Client { get; internal set; }
        public SocketGuildUser User { get; internal set; }
        public SocketGuild Guild { get; internal set; }
        public ISocketMessageChannel Channel { get; internal set; }
        public SocketUserMessage Message { get; internal set; }
        public CommandService Commands { get; internal set; }
        public IServiceProvider Services { get; internal set; }
        public DbService _db { get; internal set; }
        public MiscService _misc { get; internal set; }
        public FakeConsole Console { get; internal set; }
        public Random Random { get; internal set; }
        public LavaShardClient _manager { get; internal set; }
        public LavaRestClient _lavaRestClient { get; internal set; }
        public ImageService _img { get; set; }
        public string[] Imports { get; internal set; } = new string[]
        {
            "System",
            "System.Collections",
            "System.Collections.Generic",
            "System.Linq",
            "System.Text",
            "System.Threading.Tasks",
            "Discord",
            "Discord.Commands",
            "Discord.WebSocket",
            "Discord.API",
            "Discord.Rest",
            "Discord.Addons.Interactive",
            "System.Diagnostics",
            "Microsoft.CodeAnalysis.CSharp.Scripting",
            "Microsoft.CodeAnalysis.Scripting",
            "System.Reflection",
            "OscarBot",
            "OscarBot.Classes",
            "OscarBot.Modules",
            "OscarBot.Services",
            "OscarBot.TypeReaders",
            "System.Net",
            "Newtonsoft.Json",
            "Newtonsoft.Json.Linq",
            "System.Numerics",
            "Microsoft.EntityFrameworkCore",
            "Microsoft.Extensions.DependencyInjection",
            "Victoria",
            "Victoria.Entities",
            "Victoria.Helpers",
            "Victoria.Queue",
            "System.IO"
        };
    }
}
