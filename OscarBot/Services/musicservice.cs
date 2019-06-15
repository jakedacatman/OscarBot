using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using Discord.Commands;
using System.Threading.Tasks;
using OscarBot.Classes;
using Discord.WebSocket;
using Discord;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OscarBot.Services
{
    public class MusicService
    {
        public int SongsQueued { get { return _queues.Select(x => x.Value.Queue.Count).Sum(); } }

        private readonly DiscordShardedClient _client;
        private readonly MiscService _misc;
        private readonly AudioService _audio;

        private ConcurrentDictionary<ulong, QueueAndSkips> _queues = new ConcurrentDictionary<ulong, QueueAndSkips>();

        public MusicService(DiscordShardedClient client, MiscService misc, AudioService audio)
        {
            _client = client;
            _misc = misc;
            _audio = audio;
            _audio.TrackEnd += TrackEnd;
        }

        public Queue<Song> GetQueue(ShardedCommandContext context)
        {
            var queue = _queues.GetOrAdd(context.Guild.Id, x => new QueueAndSkips(context.Guild.Id, context.Channel.Id));
            return queue.Queue;
        }
        public Queue<Song> GetQueue(ulong guildId)
        {
            _queues.TryGetValue(guildId, out QueueAndSkips queue);
            return queue.Queue;
        }
        public ulong GetChannelId(ulong guildId)
        {
            _queues.TryGetValue(guildId, out QueueAndSkips queue);
            return queue.ChannelId;
        }

        public List<Skip> GetSkips(ShardedCommandContext context)
        {
            var queue = _queues.GetOrAdd(context.Guild.Id, x => new QueueAndSkips(context.Guild.Id, context.Channel.Id));
            return queue.Skips;
        }

        private QueueAndSkips GetQueueForGuild(ShardedCommandContext context)
        {
            var queue = _queues.GetOrAdd(context.Guild.Id, x => new QueueAndSkips(context.Guild.Id, context.Channel.Id));
            return queue;
        }
        private QueueAndSkips GetQueueForGuild(ulong guildId)
        {
            _queues.TryGetValue(guildId, out QueueAndSkips queue);
            return queue;
        }
        private Song Dequeue(ulong guildId)
        {
            _queues.TryGetValue(guildId, out QueueAndSkips queue);
            return queue.Queue.Dequeue();
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

            var gq = GetQueueForGuild(context);
            gq.Queue.Enqueue(s);

            await context.Channel.SendMessageAsync(embed: songEmbed.Build());
        }

        private void AddSkip(ShardedCommandContext context, Skip s)
        {
            var gq = GetQueueForGuild(context);
            gq.Skips.Add(s);
        }

        private void RemoveAllSkips(ShardedCommandContext context)
        {
            var gq = GetQueueForGuild(context);
            gq.Skips.RemoveAll(x => x.UserId >= 0);
        }

        public EmbedBuilder GetStats()
        {
            var s = new MusicStats(_audio.MemoryUsage, _audio.PlayingPlayers, SongsQueued);

            var fields = new List<EmbedFieldBuilder>()
            {
                new EmbedFieldBuilder().WithName("**Memory usage**").WithValue($"{s.MemoryUsage} mb").WithIsInline(false),
                new EmbedFieldBuilder().WithName("**Playing players:**").WithValue(s.PlayingPlayers).WithIsInline(true),
                new EmbedFieldBuilder().WithName("**Songs queued**").WithValue(s.SongsQueued).WithIsInline(true),
            };

            return new EmbedBuilder()
                .WithTitle($"Music Stats")
                .WithColor(_misc.RandomColor())
                .WithFields(fields)
                .WithCurrentTimestamp()
                .WithThumbnailUrl(_client.CurrentUser.GetAvatarUrl(size: 512));
        }

        public async Task PlayAsync(ShardedCommandContext context, Song s)
        {
            AudioPlayer player = _audio.GetPlayer(context.Guild.Id);
            if (player != null && player.IsPlaying) return;

            var voiceChannel = (context.User as SocketGuildUser).VoiceChannel;
            if (voiceChannel == null) return;

            player = await _audio.JoinAsync(voiceChannel, context.Channel);

            await context.Channel.SendMessageAsync(embed: GenerateNowPlaying(s).Build());

            await player.PlayAsync(s);
        }
        public async Task PlayAsync(ulong guildId, Song s)
        {
            AudioPlayer player = _audio.GetPlayer(guildId);

            await player.TextChannel.SendMessageAsync(embed: GenerateNowPlaying(s).Build());

            await player.PlayAsync(s);
        }

        public async Task SkipAsync(ShardedCommandContext context)
        {
            AudioPlayer player = _audio.GetPlayer(context.Guild.Id);
            if (player == null || !player.IsPlaying) return;

            var currUser = (SocketGuildUser)context.User;
            var users = GetSkips(context);
            if (users.Where(x => x.UserId == currUser.Id).Any()) return;

            var queue = GetQueueForGuild(context).Queue;
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

            player.Skip();
        }

        private async Task<string> GetLyricsForTrack(Song s)
        {
            string author;
            string trackTitle;
            var split = s.Name.Split('-');
            if (split.Length == 1)
            {
                author = s.Author;
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
                AudioPlayer player = _audio.GetPlayer(context.Guild.Id);
                if (player == null || !player.IsPlaying)
                {
                    await context.Channel.SendMessageAsync("There is no song playing at the moment.");
                    return false;
                }

                var cSong = player.CurrentSong;

                var lyrics = await GetLyricsForTrack(cSong);
                if (string.IsNullOrEmpty(lyrics))
                {
                    await context.Channel.SendMessageAsync("I was unable to find any lyrics for the currently playing song.");
                    return false;
                }

                var em = new EmbedBuilder()
                    .WithColor(_misc.RandomColor())
                    .WithTitle($"Lyrics for {cSong.Name}")
                    .WithThumbnailUrl(cSong.Thumbnail)
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
        /*
        public async Task EqualizeAsync(ShardedCommandContext context, List<EqualizerBand> bands)
        {
            var player = _manager.GetPlayer(context.Guild.Id);
            if (player == null || !player.IsPlaying) return;

            await player.EqualizerAsync(bands);
        }
        */
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

        private async Task TrackEnd(AudioPlayer player, string reason)
        {
            if (reason != "finished") return;
            
            var vChannel = player.VoiceChannel;
            var guildId = vChannel.GuildId;
            var queue = GetQueue(guildId);
            var cId = GetChannelId(guildId);

            var msgChannel = vChannel.Guild.GetChannelAsync(cId) as ISocketMessageChannel;

            if (queue.Count == 0)
            {
                await msgChannel.SendMessageAsync("The last song in my queue has finished...");
                await _audio.LeaveAsync(player.VoiceChannel);
                return;
            }
            var users = (vChannel as SocketVoiceChannel).Users;

            if (users.Count == 1 && users.Any(x => x.Id == _client.CurrentUser.Id))
            {
                await msgChannel.SendMessageAsync("There are no users in my voice channel...");
                await _audio.LeaveAsync(player.VoiceChannel);
                return;
            }

            Dequeue(guildId);
            if (!queue.Any())
            {
                await _audio.LeaveAsync(vChannel);
                return;
            }

            await PlayAsync(player.VoiceChannel.GuildId, queue.Peek());
        }
    }
}
