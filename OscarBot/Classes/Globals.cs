using System;
using OscarBot.Services;
using Discord.Commands;
using Discord.WebSocket;


namespace OscarBot.Classes
{
    public class Globals
    {
        public ShardedCommandContext _context { get; internal set; }
        public DiscordShardedClient _client { get; internal set; }
        public SocketGuildUser _user { get; internal set; }
        public SocketGuild _guild { get; internal set; }
        public ISocketMessageChannel _channel { get; internal set; }
        public SocketUserMessage _message { get; internal set; }
        public CommandService _commands { get; internal set; }
        public IServiceProvider _services { get; internal set; }
        public DbService _db { get; internal set; }
        public MiscService _misc { get; internal set; }
        public FakeConsole Console { get; internal set; }
        public Random Random { get; internal set; }
        public AudioService _audio { get; internal set; }
        public ImageService _img { get; internal set; }
        public MusicService _ms { get; internal set; }
        public string[] Imports { get; internal set; } = new string[]
        {
            "System",
            "System.Collections",
            "System.Collections.Generic",
            "System.Linq",
            "System.Text",
            "System.Text.RegularExpressions",
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
            "System.IO",
            "YoutubeExplode",
            "YoutubeExplode.Models",
            "YoutubeExplode.Models.MediaStreams",
            "System.Drawing",
            "System.Drawing.Imaging"
        };
    }
}
