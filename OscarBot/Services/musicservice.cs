using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using Discord.Commands;
using System.Threading.Tasks;
using OscarBot.Classes;
using Discord.WebSocket;
using Discord;
using Microsoft.EntityFrameworkCore;
using SharpLink;
using Microsoft.Extensions.DependencyInjection;


namespace OscarBot.Services
{
    public class MusicService
    {
        private readonly DiscordShardedClient _client;
        private readonly MiscService _misc;
        private readonly LavalinkManager _manager;
        private readonly ConcurrentDictionary<ulong, GuildQueue> _queues = new ConcurrentDictionary<ulong, GuildQueue>();

        public MusicService(DiscordShardedClient client, MiscService misc, LavalinkManager manager)
        {
            _client = client;
            _misc = misc;
            _manager = manager;
        }

        public Queue<Song> GetQueue(ShardedCommandContext context)
        {
            var queue = _queues.GetOrAdd(context.Guild.Id, x => new GuildQueue(context.Guild.Id, context.Channel.Id));
            return queue.Queue;
        }
        public Queue<Song> GetQueue(ulong guildId)
        {
            _queues.TryGetValue(guildId, out GuildQueue queue);
            return queue.Queue;
        }
        public ulong GetChannelId(ulong guildId)
        {
            _queues.TryGetValue(guildId, out GuildQueue queue);
            return queue.ChannelId;
        }

        public List<Skip> GetSkips(ShardedCommandContext context)
        {
            var queue = _queues.GetOrAdd(context.Guild.Id, x => new GuildQueue(context.Guild.Id, context.Channel.Id));
            return queue.Skipped;
        }

        private GuildQueue GetGuildQueue(ShardedCommandContext context)
        {
            var queue = _queues.GetOrAdd(context.Guild.Id, x => new GuildQueue(context.Guild.Id, context.Channel.Id));
            return queue;
        }
        private GuildQueue GetGuildQueue(ulong guildId)
        {
            _queues.TryGetValue(guildId, out GuildQueue queue);
            return queue;
        }

        public async Task AddSongAsync(ShardedCommandContext context, Song s)
        {
            var fields = new List<EmbedFieldBuilder>()
            {
                new EmbedFieldBuilder().WithName("**Title:**").WithValue(s.Name).WithIsInline(false),
                new EmbedFieldBuilder().WithName("**Length:**").WithValue(s.Length).WithIsInline(true),
                new EmbedFieldBuilder().WithName("**Author:**").WithValue(s.Author).WithIsInline(true),
                new EmbedFieldBuilder().WithName("**URL:**").WithValue($"<{s.URL}>").WithIsInline(false)
            };

            var songEmbed = new EmbedBuilder()
                .WithColor(_misc.RandomColor())
                .WithTitle("Song added!")
                .WithThumbnailUrl(s.Thumbnail)
                .WithFields(fields)
                .WithCurrentTimestamp();

            var gq = GetGuildQueue(context);
            gq.Queue.Enqueue(s);

            await context.Channel.SendMessageAsync(embed: songEmbed.Build());
        }

        private void AddSkip(ShardedCommandContext context, Skip s)
        {
            var gq = GetGuildQueue(context);
            gq.Skipped.Add(s);
        }

        private void RemoveAllSkips(ShardedCommandContext context)
        {
            var gq = GetGuildQueue(context);
            gq.Skipped.RemoveAll(x => x.UserId >= 0);
        }

        public Song Dequeue(ShardedCommandContext context)
        {
            var gq = GetGuildQueue(context);
            return gq.Queue.Dequeue();
        }
        public Song Dequeue(ulong guildId)
        {
            var gq = GetGuildQueue(guildId);
            return gq.Queue.Dequeue();
        }

        public async Task<bool> PlayAsync(ShardedCommandContext context, Song s)
        {
            var player = _manager.GetPlayer(context.Guild.Id);
            if (player != null && player.Playing) return false;

            var voiceChannel = (context.User as SocketGuildUser).VoiceChannel;

            if (voiceChannel == null) return false;
            else
            {
                await voiceChannel.ConnectAsync(false, false, true);
                await voiceChannel.DisconnectAsync();
                player = null; // cheap hack to make a new player
            }

            if (player == null && context.Guild.CurrentUser.VoiceChannel != null)
                await context.Guild.CurrentUser.VoiceChannel.DisconnectAsync();

            player = await _manager.JoinAsync(voiceChannel);

            var tracks = await _manager.GetTracksAsync(s.URL);
            var track = tracks.Tracks.Where(x => x.Url != null).FirstOrDefault();

            await context.Channel.SendMessageAsync(embed: GenerateNowPlaying(s).Build());

            await player.SetVolumeAsync(100);

            var timeout = Task.Delay(track.Length);

            var p = new TaskCompletionSource<bool>();
            Task TrackFinish(LavalinkPlayer _, LavalinkTrack __, string ___)
            {
                p.SetResult(true);
                return Task.CompletedTask;
            }
            _manager.TrackEnd += TrackFinish;
            await player.PlayAsync(track);
            var t = await Task.WhenAny(p.Task, timeout);
            _manager.TrackEnd -= TrackFinish;

            if (t == timeout)
            {
                Console.WriteLine($"Failed to finish playing track {track.Url}");
                return false;
            }
            else
            {
                Console.WriteLine($"Finished playing track {track.Url}");
                return true;
            }
        }

        public async Task SkipAsync(ShardedCommandContext context)
        {
            var player = _manager.GetPlayer(context.Guild.Id);
            if (player == null || !player.Playing) return;

            if (!player.CurrentTrack.IsSeekable)
            {
                await context.Channel.SendMessageAsync("This song cannot be skipped.");
                return;
            }

            var currUser = (SocketGuildUser)context.User;
            var users = GetSkips(context);
            if (users.Where(x => x.UserId == currUser.Id).Any()) return;

            var queue = GetGuildQueue(context).Queue;
            var currPlaying = queue.First();

            AddSkip(context, new Skip { SongUrl = currPlaying.URL, UserId = currUser.Id });

            if (currUser.Id == currPlaying.QueuerId)
            {
                RemoveAllSkips(context);
                await context.Channel.SendMessageAsync("Skipped the current track.");
            }
            else if (users.Count >= (player.VoiceChannel as SocketVoiceChannel).Users.Count / 3d)
            {
                RemoveAllSkips(context);
                await context.Channel.SendMessageAsync("Skipped the current track.");
            }
            else
            {
                await context.Channel.SendMessageAsync($"{context.User} voted to skip the current track.");
                return;
            }

            Dequeue(context);
            var song = queue.First();
            await PlayAsync(context, song);
        }

        public EmbedBuilder GenerateNowPlaying(Song song)
        {
            var songFields = new List<EmbedFieldBuilder>()
            {
                new EmbedFieldBuilder().WithName("**Title:**").WithValue(song.Name).WithIsInline(false),
                new EmbedFieldBuilder().WithName("**Length:**").WithValue(song.Length).WithIsInline(true),
                new EmbedFieldBuilder().WithName("**Author:**").WithValue(song.Author).WithIsInline(true),
                new EmbedFieldBuilder().WithName("**URL:**").WithValue($"<{song.URL}>").WithIsInline(false)
            };

            var nowPlaying = new EmbedBuilder()
                .WithColor(_misc.RandomColor())
                .WithTitle("Now playing:")
                .WithThumbnailUrl(song.Thumbnail)
                .WithFields(songFields)
                .WithCurrentTimestamp()
                .WithFooter($"Queued by {_client.Guilds.Where(x => x.Id == song.GuildId).First().Users.Where(x => x.Id == song.QueuerId).First()}");

            return nowPlaying;
        }

        public EmbedBuilder GenerateAddedMsg(Song s)
        {
            var fields = new List<EmbedFieldBuilder>()
            {
                new EmbedFieldBuilder().WithName("**Title:**").WithValue(s.Name).WithIsInline(false),
                new EmbedFieldBuilder().WithName("**Length:**").WithValue(s.Length).WithIsInline(true),
                new EmbedFieldBuilder().WithName("**Author:**").WithValue(s.Author).WithIsInline(true),
                new EmbedFieldBuilder().WithName("**URL:**").WithValue($"<{s.URL}>").WithIsInline(false)
            };

            var songEmbed = new EmbedBuilder()
                .WithColor(_misc.RandomColor())
                .WithTitle("Song added!")
                .WithThumbnailUrl(s.Thumbnail)
                .WithFields(fields)
                .WithCurrentTimestamp();
            return songEmbed;
        }
    }
}
