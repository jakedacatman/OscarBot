using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using Discord.Commands;
using System.Threading.Tasks;
using OscarBot.Classes;
using Discord.WebSocket;
using Discord;
using Victoria;
using Victoria.Entities;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OscarBot.Services
{
    public class MusicService
    {
        private readonly DiscordShardedClient _client;
        private readonly MiscService _misc;
        private readonly LavaShardClient _manager;
        private readonly LavaRestClient _lavaRestClient;
        private readonly ConcurrentDictionary<ulong, GuildQueue> _queues = new ConcurrentDictionary<ulong, GuildQueue>();
        private ServerStats _stats = null;

        public MusicService(DiscordShardedClient client, MiscService misc, LavaShardClient manager, LavaRestClient lavaRestClient)
        {
            _client = client;
            _misc = misc;
            _manager = manager;
            _lavaRestClient = lavaRestClient;
            _manager.OnTrackFinished += TrackEnd;
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

        public EmbedBuilder GetStats()
        {
            if (_stats == null)
            {
                return new EmbedBuilder()
                    .WithColor(_misc.RandomColor())
                    .WithTitle("Lavalink Stats")
                    .WithDescription("No stats have been received yet.")
                    .WithCurrentTimestamp();
            }
            var statsFields = new List<EmbedFieldBuilder>()
                {
                    new EmbedFieldBuilder().WithName("CPU:").WithValue(_stats.Cpu).WithIsInline(true),
                    new EmbedFieldBuilder().WithName("Percentage used of allocated memory:").WithValue(Math.Round(_stats.Memory.Used / (double)_stats.Memory.Allocated)).WithIsInline(true),
                    new EmbedFieldBuilder().WithName("Players:").WithValue($"{_stats.PlayerCount} ({_stats.PlayingPlayers} playing)").WithIsInline(true),
                    new EmbedFieldBuilder().WithName("Uptime:").WithValue($"{_stats.Uptime:g}").WithIsInline(true),
                    new EmbedFieldBuilder().WithName("Frames sent:").WithValue(_stats.Frames.Sent).WithIsInline(true),
                };

            var em = new EmbedBuilder()
                .WithTitle("Lavalink Stats")
                .WithCurrentTimestamp()
                .WithFields(statsFields);
            return em;
        }

        public async Task PlayAsync(ShardedCommandContext context, Song s)
        {
            var player = _manager.GetPlayer(context.Guild.Id);
            if (player != null && player.IsPlaying) return;

            var voiceChannel = (context.User as SocketGuildUser).VoiceChannel;
            if (voiceChannel == null) return;

            player = await _manager.ConnectAsync(voiceChannel, (ITextChannel)context.Channel);

            var tracks = (await _lavaRestClient.SearchTracksAsync(s.URL)).Tracks.Where(x => x.Uri != null);
            if (!tracks.Any())
            {
                await context.Channel.SendMessageAsync("I was unable to grab a track for the requested song.");
                return;
            }
            var track = tracks.First();

            await context.Channel.SendMessageAsync(embed: GenerateNowPlaying(s).Build());

            await player.PlayAsync(track);
            await player.SetVolumeAsync(100);
        }
        public async Task PlayAsync(ulong guildId, Song s)
        {
            var player = _manager.GetPlayer(guildId);

            var tracks = (await _lavaRestClient.SearchTracksAsync(s.URL)).Tracks.Where(x => x.Uri != null);
            if (!tracks.Any())
            {
                await player.TextChannel.SendMessageAsync("I was unable to grab a track for the requested song.");
                await _manager.DisconnectAsync(player.VoiceChannel);
                return;
            }
            var track = tracks.First();

            await player.TextChannel.SendMessageAsync(embed: GenerateNowPlaying(s).Build());

            await player.PlayAsync(track);
            await player.SetVolumeAsync(100);
        }

        public async Task SkipAsync(ShardedCommandContext context)
        {
            var player = _manager.GetPlayer(context.Guild.Id);
            if (player == null || !player.IsPlaying) return;

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
            else if (users.Count >= ((player.VoiceChannel as SocketVoiceChannel).Users.Count - 1) / 3d)
            {
                RemoveAllSkips(context);
                await context.Channel.SendMessageAsync("Skipped the current track.");
            }
            else
            {
                await context.Channel.SendMessageAsync($"{context.User} voted to skip the current track.");
                return;
            }

            TimeSpan.TryParse(currPlaying.Length, out var ts);
            await player.SeekAsync(ts.Subtract(TimeSpan.FromSeconds(1)));
        }

        private async Task<string> GetLyricsForTrack(LavaTrack track)
        {
            string author;
            string trackTitle;
            var split = track.Title.Split('-');
            if (split.Length == 1)
            {
                author = track.Author;
                trackTitle = split[0];
            }
            else
            {
                author = split[0].Substring(0, split[0].Length - 1);
                trackTitle = split[1].Substring(1);
            }

            using (var cl = new WebClient())
            {
                var result = JsonConvert.DeserializeObject<JObject>(await cl.DownloadStringTaskAsync(Uri.EscapeUriString($"https://api.lyrics.ovh/v1/{author}/{trackTitle}")));
                return result.SelectToken("lyrics") != null ? result.SelectToken("lyrics").Value<string>() : string.Empty;
            }
        }

        public async Task<bool> GetLyricsAsync(ShardedCommandContext context)
        {
            try
            {
                var player = _manager.GetPlayer(context.Guild.Id);
                if (player == null || !player.IsPlaying)
                {
                    await context.Channel.SendMessageAsync("There is no song playing at the moment.");
                    return false;
                }

                var cTrack = player.CurrentTrack;

                var lyrics = await GetLyricsForTrack(cTrack);
                if (string.IsNullOrEmpty(lyrics))
                {
                    await context.Channel.SendMessageAsync("I was unable to find any lyrics for the currently playing song.");
                    return false;
                }

                var em = new EmbedBuilder()
                    .WithColor(_misc.RandomColor())
                    .WithTitle($"Lyrics for {cTrack.Title}")
                    .WithThumbnailUrl(await cTrack.FetchThumbnailAsync())
                    .WithCurrentTimestamp();

                if (lyrics.Length > 1000)
                    em = em.WithUrl(await _misc.UploadToBisogaAsync(lyrics));
                else
                    em = em.WithDescription(lyrics);

                await context.Channel.SendMessageAsync(embed: em.Build());

                return true;
            }
            catch
            {
                await context.Channel.SendMessageAsync("I was unable to find any lyrics for the currently playing song.");
                return false;
            }
        }

        public async Task EqualizeAsync(ShardedCommandContext context, List<EqualizerBand> bands)
        {
            var player = _manager.GetPlayer(context.Guild.Id);
            if (player == null || !player.IsPlaying) return;

            await player.EqualizerAsync(bands);
        }

        private EmbedBuilder GenerateNowPlaying(Song song)
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

        private EmbedBuilder GenerateAddedMsg(Song s)
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

        private Task RefreshStats(ServerStats stats)
        {
            _stats = stats;
            return Task.CompletedTask;
        }

        private async Task TrackEnd(LavaPlayer player, LavaTrack oldTrack, TrackEndReason reason)
        {
            if (!reason.ShouldPlayNext()) return;
            
            var vChannel = player.VoiceChannel;
            var guildId = vChannel.GuildId;
            var queue = GetQueue(guildId);
            var cId = GetChannelId(guildId);

            var msgChannel = vChannel.Guild.GetChannelAsync(cId) as ISocketMessageChannel;

            if (queue.Count == 0)
            {
                await msgChannel.SendMessageAsync("The last song in my queue has finished...");
                await _manager.DisconnectAsync(player.VoiceChannel);
                return;
            }
            var users = (vChannel as SocketVoiceChannel).Users;

            if (users.Count == 1 && users.Any(x => x.Id == _client.CurrentUser.Id))
            {
                await msgChannel.SendMessageAsync("There are no users in my voice channel...");
                await _manager.DisconnectAsync(player.VoiceChannel);
                return;
            }

            Dequeue(guildId);
            if (!queue.Any())
            {
                await _manager.DisconnectAsync(vChannel);
                return;
            }

            await PlayAsync(player.VoiceChannel.GuildId, queue.First());
            await player.SetVolumeAsync(100);
            List<EqualizerBand> eBands = new List<EqualizerBand>();
            for (int i = 0; i < 15; i++)
                eBands.Add(new EqualizerBand { Band = (ushort)i, Gain = 0 });

            await player.EqualizerAsync(eBands);
        }
    }
}
