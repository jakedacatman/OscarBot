using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Discord.Addons.Interactive;
using SharpLink;
using SharpLink.Stats;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OscarBot.Classes;
using OscarBot.Services;
using Microsoft.Extensions.DependencyInjection;

namespace OscarBot.Modules
{
    [Name("Music")]
    public class MusicModule : InteractiveBase<ShardedCommandContext>
    {
        private readonly LavalinkManager _manager;
        private readonly DiscordShardedClient _client;
        private readonly MiscService _misc;
        private readonly MusicService _ms;
        private readonly DbService _db;
        private readonly IServiceProvider _services;
        private LavalinkStats _stats;


        public MusicModule(LavalinkManager manager, DiscordShardedClient client, MiscService misc, MusicService ms, DbService db, IServiceProvider services)
        {
            _manager = manager;
            _client = client;
            _misc = misc;
            _ms = ms;
            _db = db;
            _services = services;
            _manager.TrackEnd += TrackEnd;
            _manager.Stats += RefreshStats;
        }

        [Command("add")]
        [Alias("play")]
        [Summary("Adds a track from YouTube to be played, and starts the queue if you are in a voice channel.")]
        public async Task AddCmd([Summary("The URL to add from YouTube, or search term."), Remainder]string urlOrTerm)
        {
            var msg = await ReplyAsync("Adding your requested track...");
            IUserMessage nowPlayingMsg;
            try
            {
                using (var scope = _services.CreateScope())
                {
                    var _cx = scope.ServiceProvider.GetRequiredService<EntityContext>();

                    Song s;
                    var key = _db.GetApiKey("youtube");
                    urlOrTerm = urlOrTerm.Trim('<', '>');

                    if (Uri.TryCreate(urlOrTerm, UriKind.Absolute, out Uri uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
                    {
                        using (WebClient client = new WebClient())
                        {
                            Regex regex = new Regex("youtu(?:.be|be.com)/(?:.*v(?:/|=)|(?:.*/)?)([a-zA-Z0-9-_]+)");
                            var id = regex.Match(urlOrTerm).Groups[1].Value;

                            var json = await client.DownloadStringTaskAsync($"https://www.googleapis.com/youtube/v3/videos?id={id}&key={key}&part=snippet,contentDetails,statistics,status");
                            JObject obj = JsonConvert.DeserializeObject<JObject>(json);

                            if (obj.SelectToken("pageInfo").SelectToken("totalResults").Value<int>() == 0) return;

                            s = new Song
                            {
                                URL = $"https://www.youtube.com/watch?v={id}",
                                QueuerId = Context.User.Id,
                                ChannelId = Context.Channel.Id,
                                GuildId = Context.Guild.Id,
                                Author = obj.SelectToken("items").First.SelectToken("snippet.channelTitle").Value<string>(),
                                Name = obj.SelectToken("items").First.SelectToken("snippet.title").Value<string>(),
                                Length = $"{System.Xml.XmlConvert.ToTimeSpan(obj.SelectToken("items").First.SelectToken("contentDetails.duration").Value<string>()):g}",
                                Thumbnail = obj.SelectToken("items").First.SelectToken("snippet.thumbnails.high.url").Value<string>()
                            };

                            await (await _ms.GetGuildQueue(Context.Guild.Id)).QueueUp(Context.Guild.Id, s, _cx);

                            await msg.ModifyAsync(x =>
                            {
                                x.Content = " ";
                                x.Embed = _ms.GenerateAddedMsg(s).Build();
                            });
                        }
                    }
                    else
                    {
                        YouTubeService y = new YouTubeService(new BaseClientService.Initializer() { ApiKey = key });

                        var request = y.Search.List("snippet");
                        request.Q = urlOrTerm;
                        request.Type = "video";
                        request.MaxResults = 10;

                        var response = await request.ExecuteAsync();

                        List<string> titles = new List<string>();
                        int counter = 1;

                        foreach (var result in response.Items)
                        {
                            titles.Add(counter.ToString() + ". " + result.Snippet.Title + $" *by {result.Snippet.ChannelTitle}*");
                            counter++;
                        }

                        EmbedBuilder embed = new EmbedBuilder()
                            .WithColor(_misc.RandomColor())
                            .WithTitle("Respond with the number that you want to add.")
                            .WithCurrentTimestamp()
                            .WithDescription($"**{string.Join("**\n**", titles)}**");

                        var message = await ReplyAsync(embed: embed.Build());
                        var nextMsg = await NextMessageAsync(timeout: TimeSpan.FromSeconds(30));
                        if (!int.TryParse(nextMsg.Content, out int number))
                        {
                            await msg.DeleteAsync();
                            await message.DeleteAsync();
                            return;
                        }
                        if (number > 10 || number < 1)
                        {
                            await msg.DeleteAsync();
                            await message.DeleteAsync();
                            return;
                        }
                        var id = response.Items[number - 1].Id.VideoId;

                        using (WebClient client = new WebClient())
                        {
                            var json = await client.DownloadStringTaskAsync($"https://www.googleapis.com/youtube/v3/videos?id={id}&key={key}&part=snippet,contentDetails,statistics,status");
                            JObject obj = JsonConvert.DeserializeObject<JObject>(json);

                            s = new Song
                            {
                                URL = $"https://www.youtube.com/watch?v={id}",
                                QueuerId = Context.User.Id,
                                ChannelId = Context.Channel.Id,
                                GuildId = Context.Guild.Id,
                                Author = obj.SelectToken("items").First.SelectToken("snippet.channelTitle").Value<string>(),
                                Name = obj.SelectToken("items").First.SelectToken("snippet.title").Value<string>(),
                                Length = $"{System.Xml.XmlConvert.ToTimeSpan(obj.SelectToken("items").First.SelectToken("contentDetails.duration").Value<string>()):g}",
                                Thumbnail = obj.SelectToken("items").First.SelectToken("snippet.thumbnails.high.url").Value<string>()
                            };
                            if (obj.SelectToken("items").First.SelectToken("snippet.title").Value<string>() == null) return;

                            await (await _ms.GetGuildQueue(Context.Guild.Id)).QueueUp(Context.Guild.Id, s, _cx);

                            await msg.ModifyAsync(x =>
                            {
                                x.Content = " ";
                                x.Embed = _ms.GenerateAddedMsg(s).Build();
                            });
                            await message.DeleteAsync();

                        }
                    }


                    var player = _manager.GetPlayer(Context.Guild.Id);
                    if (player != null && player.Playing) return;

                    var voiceChannel = (Context.User as SocketGuildUser).VoiceChannel;

                    if (voiceChannel == null) return;

                    if (player != null)
                    {
                        await player.DisconnectAsync();
                        player = null;
                    }

                    if (player == null && Context.Guild.CurrentUser.VoiceChannel != null)
                        await Context.Guild.CurrentUser.VoiceChannel.DisconnectAsync();

                    if (player == null) player = await _manager.JoinAsync(voiceChannel);

                    var tracks = await _manager.GetTracksAsync(s.URL);
                    var track = tracks.Tracks.Where(x => x.Url != null).FirstOrDefault();

                    nowPlayingMsg = await ReplyAsync(embed: _ms.GenerateNowPlaying(s).Build());

                    await player.SetVolumeAsync(100);
                    await player.PlayAsync(track);
                }

            }
            catch (System.Net.Http.HttpRequestException)
            {
                await msg.DeleteAsync();
                nowPlayingMsg = await ReplyAsync($"The bot has not connected to Lavalink yet. Please wait a few moments...");
            }
            catch (Exception e)
            {
                await msg.DeleteAsync();
                await ReplyAsync(embed: _misc.GenerateErrorMessage(e).Build());
            }
        }

        [Command("repeat")]
        [Alias("loop")]
        [Summary("Adds the currently playing song to the queue.")]
        public async Task RepeatCmd()
        {
            try
            {
                using (var scope = _services.CreateScope())
                {
                    var _db = scope.ServiceProvider.GetRequiredService<EntityContext>();

                    LavalinkPlayer player = _manager.GetPlayer(Context.Guild.Id);
                    if (player == null || player.Playing == false)
                    {
                        await ReplyAsync("No track is playing.");
                        return;
                    }

                    var s = (await _ms.GetGuildQueue(Context.Guild.Id)).First();

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

                    await (await _ms.GetGuildQueue(Context.Guild.Id)).QueueUp(Context.Guild.Id, s, _db);

                    await ReplyAsync(embed: songEmbed.Build());
                }
            }
            catch (Exception e)
            {
                await ReplyAsync(embed: _misc.GenerateErrorMessage(e).Build());
            }
        }

        [Command("current")]
        [Alias("now", "nowplaying")]
        [Summary("Gets information about the currently playing track.")]
        public async Task CurrentCmd()
        {
            try
            {
                LavalinkPlayer player = _manager.GetPlayer(Context.Guild.Id);
                var s = (await _ms.GetGuildQueue(Context.Guild.Id)).First();

                if (player == null || player.Playing == false)
                {
                    await ReplyAsync("No track is playing.");
                    return;
                }

                if (s == null)
                {
                    await ReplyAsync("No songs are queued.");
                    return;
                }

                var currPos = TimeSpan.FromMilliseconds(player.CurrentPosition);
                var len = player.CurrentTrack.Length;

                var totalLen = 40;

                var interval = Math.Round(len.TotalMilliseconds / totalLen, 2);

                var signNo = Math.Round(currPos.TotalMilliseconds / interval, 2);

                EmbedBuilder embed = new EmbedBuilder()
                    .WithColor(_misc.RandomColor())
                    .WithTitle("Now playing:")
                    .WithThumbnailUrl(s.Thumbnail)
                    .AddField("**Title:**", s.Name, false)
                    .AddField("**Position:**", $"[{ string.Join("", Enumerable.Repeat("\\|", (int)signNo)) + string.Join("", Enumerable.Repeat(".", totalLen - (int)signNo))}]\n[{currPos:g}/{len:g}]", false)
                    .AddField("**Author:**", s.Author, true)
                    .AddField("**Stream?**", $"{player.CurrentTrack.IsStream}", true)
                    .AddField("**URL:**", $"<{s.URL}>", false)
                    .WithCurrentTimestamp()
                    .WithFooter($"{(Context.User.IsQueuer(s) ? "Queued by you" : $"Queued by {Context.Client.Guilds.Where(x => x.Id == s.GuildId).First().Users.Where(x => x.Id == s.QueuerId).First()}")}");

                await ReplyAsync(embed: embed.Build());
            }
            catch (Exception e)
            {
                await ReplyAsync(embed: _misc.GenerateErrorMessage(e).Build());
            }
        }

        [Command("queue")]
        [Alias("songs", "tracks")]
        [Summary("Gets the currently queued songs.")]
        public async Task QueueCmd()
        {
            try
            {
                ICollection<Song> queue = await _ms.GetGuildQueue(Context.Guild.Id);

                List<string> titles = new List<string>();

                if (queue.Count == 0)
                    await ReplyAsync("No songs are queued yet.");
                else
                {

                    int i = 1;
                    foreach (Song s in queue)
                    {
                        if (i == 1 && _manager.GetPlayer(Context.Guild.Id) != null && _manager.GetPlayer(Context.Guild.Id).Playing)
                        {
                            titles.Add($"{i}. **{s.Name}** (queued by <@{s.QueuerId}>) -- currently playing");
                        }
                        else
                        {
                            titles.Add($"{i}. **{s.Name}** (queued by <@{s.QueuerId}>)");
                        }
                        i++;
                    }
                    var em = new EmbedBuilder()
                        .WithColor(_misc.RandomColor())
                        .WithDescription(string.Join("\n", titles))
                        .WithCurrentTimestamp()
                        .WithFooter("Powered by Lavalink and SharpLink")
                        .WithTitle("My queue");
                    await ReplyAsync("", false, em.Build());
                }

            }
            catch (Exception e)
            {
                await ReplyAsync(embed: _misc.GenerateErrorMessage(e).Build());
            }
        }

        [Command("start")]
        [Summary("Starts the queue if it has not already been started.")]
        public async Task StartCmd()
        {
            try
            {
                var player = _manager.GetPlayer(Context.Guild.Id);

                if (player != null && player.Playing) return;

                var voiceChannel = (Context.User as SocketGuildUser).VoiceChannel;

                if (voiceChannel != null)
                {
                    await voiceChannel.ConnectAsync(false, false, true);
                    await voiceChannel.DisconnectAsync();
                }
                else return;

                player = await _manager.JoinAsync(voiceChannel);

                Song song = (await _ms.GetGuildQueue(Context.Guild.Id)).First();

                if (song == null) return;

                var tracks = await _manager.GetTracksAsync(song.URL);
                var track = tracks.Tracks.Where(x => x.Url != null).FirstOrDefault();

                await ReplyAsync(embed: _ms.GenerateNowPlaying(song).Build());

                await player.SetVolumeAsync(100);
                await player.PlayAsync(track);
            }
            catch (Exception e)
            {
                await ReplyAsync(embed: _misc.GenerateErrorMessage(e).Build());
            }
        }

        [Command("volume")]
        [Alias("setvolume", "vol")]
        [Summary("Sets the volume to a certain percentage.")]
        public async Task VolumeCmd([Summary("The percentage to set the volume to.")]uint volume)
        {
            try
            {
                var player = _manager.GetPlayer(Context.Guild.Id);
                var user = Context.User;
                var song = (await _ms.GetGuildQueue(Context.Guild.Id)).First();

                if (player == null || !player.Playing) return;
                if (!(user as SocketGuildUser).GuildPermissions.DeafenMembers || !user.IsQueuer(song)) return;
                if (volume < 0u || volume > 150u) volume = 100u;

                await player.SetVolumeAsync(volume);

                await ReplyAsync($"Set volume to **{volume}%**!");

                if (!player.Playing) await player.DisconnectAsync();
            }
            catch (Exception e)
            {
                await ReplyAsync(embed: _misc.GenerateErrorMessage(e).Build());
            }
        }

        [Command("skip")]
        [Summary("Votes to skip the current track, or skips it entirely if you queued it.")]
        public async Task SkipCmd()
        {
            try
            {
                using (var scope = _services.CreateScope())
                {
                    var _db = scope.ServiceProvider.GetRequiredService<EntityContext>();

                    var currUser = (SocketGuildUser)Context.User;
                    var users = await _ms.GetSkips(Context.Guild.Id);
                    if (users.Where(x => x.UserId == currUser.Id).Count() > 0) return;

                    var player = _manager.GetPlayer(Context.Guild.Id);
                    if (player == null) return;

                    var song = (await _ms.GetGuildQueue(Context.Guild.Id)).First();
                    if (song == null) return;

                    await users.AddUser(new Skip { SongUrl = song.URL, UserId = currUser.Id }, Context.Guild.Id, _db);

                    if (currUser.Id == song.QueuerId)
                    {
                        await users.RemoveUsers(Context.Guild.Id, _db);
                        await player.SeekAsync((int)player.CurrentTrack.Length.TotalMilliseconds);
                        await ReplyAsync("Skipped the current track.");
                    }
                    else
                    {
                        await ReplyAsync($"{Context.User} voted to skip the current track.");
                    }

                    if (users.Count >= (player.VoiceChannel as SocketVoiceChannel).Users.Count / 3d)
                    {
                        await users.RemoveUsers(Context.Guild.Id, _db);
                        await player.SeekAsync((int)player.CurrentTrack.Length.TotalMilliseconds);
                        await ReplyAsync("Skipped the current track.");
                    }
                }
            }
            catch (Exception e)
            {
                await ReplyAsync(embed: _misc.GenerateErrorMessage(e).Build());
            }
        }

        [Command("skips")]
        [Summary("Displays the amount of skips the currently-playing track has.")]
        public async Task SkipsCmd()
        {
            try
            {
                var skips = await _ms.GetSkips(Context.Guild.Id);
                if (skips.Count() == 0) return;

                var users = new List<SocketGuildUser>();

                foreach (var s in skips)
                    users.Add(Context.Guild.Users.Single(x => x.Id == s.UserId));

                EmbedBuilder em = new EmbedBuilder()
                    .WithTitle("Current votes:")
                    .WithColor(_misc.RandomColor())
                    .WithDescription(string.Join(", ", users))
                    .WithFooter(skips.Count().ToString())
                    .WithCurrentTimestamp();

                await ReplyAsync(embed: em.Build());
            }
            catch (Exception e)
            {
                await ReplyAsync(embed: _misc.GenerateErrorMessage(e).Build());
            }
        }

        [Command("stats")]
        [Summary("Views the stats of Lavalink, the music service.")]
        public async Task StatsCmd()
        {
            try
            {
                var stats = _stats;
                if (stats == null)
                {
                    await ReplyAsync("No stats have been received yet.");
                    return;
                }
                var statsFields = new List<EmbedFieldBuilder>()
                {
                    new EmbedFieldBuilder().WithName("CPU:").WithValue(stats.CPU.LavalinkLoad).WithIsInline(true),
                    new EmbedFieldBuilder().WithName("Percentage used of allocated memory:").WithValue(Math.Round(stats.Memory.Used / (double)stats.Memory.Allocated)).WithIsInline(true),
                    new EmbedFieldBuilder().WithName("Players:").WithValue($"{stats.Players} ({stats.PlayingPlayers} playing)").WithIsInline(true),
                    new EmbedFieldBuilder().WithName("Uptime:").WithValue($"{TimeSpan.FromMilliseconds(stats.Uptime):g}").WithIsInline(true),
                    new EmbedFieldBuilder().WithName("Frames sent:").WithValue(stats.FrameStats.Sent).WithIsInline(true),
                };

                var em = new EmbedBuilder()
                    .WithTitle("Lavalink Stats")
                    .WithCurrentTimestamp()
                    .WithFields(statsFields);

                await ReplyAsync(embed: em.Build());
            }
            catch (Exception e)
            {
                await ReplyAsync(embed: _misc.GenerateErrorMessage(e).Build());
            }
        }

        private Task RefreshStats(LavalinkStats stats)
        {
            _stats = stats;
            return Task.CompletedTask;
        }

        private async Task TrackEnd(LavalinkPlayer player, LavalinkTrack oldTrack, string reason)
        {
            using (var scope = _services.CreateScope())
            {
                var _db = scope.ServiceProvider.GetRequiredService<EntityContext>();

                var queue = await _ms.GetGuildQueue(player.VoiceChannel.GuildId);
                var song = await queue.Pop(Context.Guild.Id, _db);

                var msgChannel = player.VoiceChannel.Guild.GetChannelAsync(song.ChannelId) as ISocketMessageChannel;

                if (song == null)
                {
                    await msgChannel.SendMessageAsync("The last song in my queue has finished...");
                    await player.DisconnectAsync();
                    return;
                }

                if ((player.VoiceChannel as SocketVoiceChannel).Users.Count == 1 && (player.VoiceChannel as SocketVoiceChannel).Users.First().Id == Context.Client.CurrentUser.Id)
                {
                    await msgChannel.SendMessageAsync("There are no users in my voice channel...");
                    await player.DisconnectAsync();
                    return;
                }
                var tracks = await _manager.GetTracksAsync(song.URL);

                var track = tracks.Tracks.First();

                await ReplyAsync(embed: _ms.GenerateNowPlaying(song).Build());

                await player.PlayAsync(track);
                await player.SetVolumeAsync(100);
            }
        }
    }
    
}
