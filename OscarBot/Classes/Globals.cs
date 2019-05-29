using System;
using OscarBot.Services;
using Discord.Commands;
using Discord.WebSocket;

namespace OscarBot.Classes
{
    public class Globals
    {
        public ShardedCommandContext Context { get; internal set; }
        public DiscordShardedClient Client { get; internal set; }
        public SocketGuildUser User { get; internal set; }
        public SocketGuild Guild { get; internal set; }
        public SocketGuildChannel Channel { get; internal set; }
        public SocketUserMessage Message { get; internal set; }
        public CommandService Commands { get; internal set; }
        public IServiceProvider Services { get; internal set; }
        public DbService _db { get; internal set; }
        public FakeConsole Console { get; internal set; }
        public Random Random = new Random();
        public string[] Imports = new string[]
        {
            "System",
            "System.Collections.Generic",
            "System.Linq",
            "System.Text",
            "System.Threading.Tasks",
            "Discord",
            "Discord.Commands",
            "Discord.WebSocket",
            "Discord.Addons.Interactive",
            "System.Diagnostics",
            "Microsoft.CodeAnalysis.CSharp.Scripting",
            "Microsoft.CodeAnalysis.Scripting",
            "System.Reflection",
            "OscarBot.Classes",
            "OscarBot.Modules",
            "OscarBot.Services",
            "OscarBot.TypeReaders",
            "System.Net",
            "Newtonsoft.Json",
            "Newtonsoft.Json.Linq",
            "System.Numerics",
            "Microsoft.EntityFrameworkCore"
        };
    }
}
