﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Discord.Addons.Interactive;
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
        private readonly AudioService _audio;
        private readonly DiscordShardedClient _client;
        private readonly MiscService _misc;
        private readonly MusicService _ms;
        private readonly DbService _db;
        private readonly IServiceProvider _services;


        public MusicModule(AudioService audio, DiscordShardedClient client, MiscService misc, MusicService ms, DbService db, IServiceProvider services)
        {
            _audio = audio;
            _client = client;
            _misc = misc;
            _ms = ms;
            _db = db;
            _services = services;
        }
        [Command("stats")]
        [Summary("Gets the music stats.")]
        public async Task StatsCmd()
        {
            try
            {
                await ReplyAsync(embed: _ms.GetStats().Build());
            }
            catch (Exception e)
            {
                await ReplyAsync(embed: (await _misc.GenerateErrorMessage(e)).Build());
            }
        }

        [Command("add")]
        [Alias("play")]
        [Summary("Adds a track from YouTube to be played, and starts the queue if you are in a voice channel.")]
        public async Task AddCmd([Summary("The URL to add from YouTube, or search term."), Remainder]string urlOrTerm)
        {
            try
            {
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
                        var songUrl = $"https://www.youtube.com/watch?v={id}";

                        var pleasewait = await ReplyAsync($"Searching for your track... <a:search:588920003374350356>");
                        var streaminfo = await _audio.GetStreamInfo(id);

                        s = new Song
                        {
                            URL = songUrl,
                            AudioURL = streaminfo.Url,
                            Bitrate = streaminfo.Bitrate,
                            QueuerId = Context.User.Id,
                            ChannelId = Context.Channel.Id,
                            GuildId = Context.Guild.Id,
                            Author = obj.SelectToken("items").First.SelectToken("snippet.channelTitle").Value<string>(),
                            Name = obj.SelectToken("items").First.SelectToken("snippet.title").Value<string>(),
                            Length = System.Xml.XmlConvert.ToTimeSpan(obj.SelectToken("items").First.SelectToken("contentDetails.duration").Value<string>()),
                            Thumbnail = obj.SelectToken("items").First.SelectToken("snippet.thumbnails.high.url").Value<string>()
                        };

                        await pleasewait.DeleteAsync();
                        await _ms.AddSongAsync(Context, s);
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
                        titles.Add($"{counter}. {result.Snippet.Title} *by {result.Snippet.ChannelTitle}*");
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
                        await message.DeleteAsync();
                        return;
                    }
                    if (number > 10 || number < 1)
                    {
                        await message.DeleteAsync();
                        return;
                    }
                    var id = response.Items[number - 1].Id.VideoId;

                    using (WebClient client = new WebClient())
                    {
                        var json = await client.DownloadStringTaskAsync($"https://www.googleapis.com/youtube/v3/videos?id={id}&key={key}&part=snippet,contentDetails,statistics,status");
                        JObject obj = JsonConvert.DeserializeObject<JObject>(json);
                        var songUrl = $"https://www.youtube.com/watch?v={id}";

                        var pleasewait = await ReplyAsync($"Searching for your track... <a:search:588920003374350356>");
                        var streaminfo = await _audio.GetStreamInfo(id);

                        s = new Song
                        {
                            URL = songUrl,
                            AudioURL = streaminfo.Url,
                            Bitrate = streaminfo.Bitrate,
                            QueuerId = Context.User.Id,
                            ChannelId = Context.Channel.Id,
                            GuildId = Context.Guild.Id,
                            Author = obj.SelectToken("items").First.SelectToken("snippet.channelTitle").Value<string>(),
                            Name = obj.SelectToken("items").First.SelectToken("snippet.title").Value<string>(),
                            Length = System.Xml.XmlConvert.ToTimeSpan(obj.SelectToken("items").First.SelectToken("contentDetails.duration").Value<string>()),
                            Thumbnail = obj.SelectToken("items").First.SelectToken("snippet.thumbnails.high.url").Value<string>()
                        };
                        if (obj.SelectToken("items").First.SelectToken("snippet.title").Value<string>() == null) return;

                        await pleasewait.DeleteAsync();
                        await message.DeleteAsync();

                        await _ms.AddSongAsync(Context, s);
                    }
                }

                await _ms.PlayAsync(Context, s);
            }
            catch (System.Net.Http.HttpRequestException)
            {
                await ReplyAsync($"The bot has not connected to Lavalink yet. Please wait a few moments...");
            }
            catch (Exception e)
            {
                await ReplyAsync(embed: (await _misc.GenerateErrorMessage(e)).Build());
            }
        }

        [Command("repeat")]
        [Alias("loop")]
        [Summary("Adds the currently playing song to the queue.")]
        public async Task RepeatCmd()
        {
            try
            {
                AudioPlayer player = _audio.GetPlayer(Context.Guild.Id);
                if (player == null || player.IsPlaying == false)
                {
                    await ReplyAsync("No track is playing.");
                    return;
                }

                var s = _ms.GetQueue(Context).First();
                await _ms.AddSongAsync(Context, s);
            }
            catch (Exception e)
            {
                await ReplyAsync(embed: (await _misc.GenerateErrorMessage(e)).Build());
            }
        }
        
        [Command("current")]
        [Alias("now", "nowplaying")]
        [Summary("Gets information about the currently playing track.")]
        public async Task CurrentCmd()
        {
            try
            {
                AudioPlayer player = _audio.GetPlayer(Context.Guild.Id);
                var queue = _ms.GetQueue(Context);
                if (!queue.Any()) return;
                var s = queue.Peek();

                if (player == null || player.IsPlaying == false)
                {
                    await ReplyAsync("No track is playing.");
                    return;
                }

                if (s == null)
                {
                    await ReplyAsync("No songs are queued.");
                    return;
                }

                var currPos = player.CurrentPosition;
                var len = player.CurrentSong.Length;

                var totalLen = 40;
                var interval = Math.Round(len.TotalMilliseconds / totalLen, 2);
                var signNo = (int)Math.Round(currPos.TotalMilliseconds / interval, 2);

                EmbedBuilder embed = new EmbedBuilder()
                    .WithColor(_misc.RandomColor())
                    .WithTitle("Now playing:")
                    .WithThumbnailUrl(s.Thumbnail)
                    .AddField("**Title:**", s.Name, false)
                    .AddField("**Position:**", $"[{ string.Join("", Enumerable.Repeat("\\|", signNo)) + string.Join("", Enumerable.Repeat(".", totalLen - signNo))}]\n({currPos:g}/{len:g}) ", false)
                    .AddField("**Author:**", s.Author, true)
                    .AddField("**Added at:**", $"{player.CurrentSong.AddedAt:g}", true)
                    .AddField("**URL:**", $"<{s.URL}>", false)
                    .WithCurrentTimestamp()
                    .WithFooter($"{(Context.User.IsQueuer(s) ? "Queued by you" : $"Queued by {Context.Client.Guilds.Where(x => x.Id == s.GuildId).First().Users.Where(x => x.Id == s.QueuerId).First()}")}");

                await ReplyAsync(embed: embed.Build());
            }
            catch (Exception e)
            {
                await ReplyAsync(embed: (await _misc.GenerateErrorMessage(e)).Build());
            }
        }
        

        [Command("queue")]
        [Alias("songs", "tracks")]
        [Summary("Gets the currently queued songs.")]
        public async Task QueueCmd()
        {
            try
            {
                Queue<Song> queue = _ms.GetQueue(Context);

                List<string> titles = new List<string>();

                if (queue.Count == 0)
                    await ReplyAsync("No songs are queued yet.");
                else
                {

                    int i = 1;
                    foreach (Song s in queue.Take(10))
                    {
                        if (i == 1 && _audio.GetPlayer(Context.Guild.Id) != null && _audio.GetPlayer(Context.Guild.Id).IsPlaying)
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
                        .WithFooter($"{queue.Count} total songs")
                        .WithCurrentTimestamp()
                        .WithTitle("First 10 songs");
                    await ReplyAsync("", false, em.Build());
                }

            }
            catch (Exception e)
            {
                await ReplyAsync(embed: (await _misc.GenerateErrorMessage(e)).Build());
            }
        }

        [Command("start")]
        [Summary("Starts the queue if it has not already been started.")]
        public async Task StartCmd()
        {
            try
            {
                Song song;
                var queue = _ms.GetQueue(Context);
                if (queue.Count == 0) return;
                song = queue.First();

                if (song == null) return;

                await _ms.PlayAsync(Context, song);
            }
            catch (Exception e)
            {
                await ReplyAsync(embed: (await _misc.GenerateErrorMessage(e)).Build());
            }
        }

        [Command("volume")]
        [Alias("setvolume", "vol")]
        [Summary("Sets the volume to a certain percentage.")]
        public async Task VolumeCmd([Summary("The percentage to set the volume to.")]uint volume)
        {
            try
            {
                AudioPlayer player = _audio.GetPlayer(Context.Guild.Id);
                var user = Context.User;
                var song = _ms.GetQueue(Context).First();

                if (player == null || !player.IsPlaying) return;
                if (user.IsQueuer(song) || (user as SocketGuildUser).GuildPermissions.DeafenMembers)
                {
                    if (volume < 0 || volume > 100000) volume = 100;

                    player.SetVolume(volume);

                    await ReplyAsync($"Set volume to **{volume}%**!");
                }
            }
            catch (Exception e)
            {
                await ReplyAsync(embed: (await _misc.GenerateErrorMessage(e)).Build());
            }
        }

        [Command("lyrics")]
        [Summary("Gets the lyrics to the currently playing track.")]
        public async Task LyricsCmd()
        {
            try
            {
                await _ms.GetLyricsAsync(Context);
            }
            catch (Exception e)
            {
                await ReplyAsync(embed: (await _misc.GenerateErrorMessage(e)).Build());
            }
        }
        /*
        [Command("bass")]
        [Alias("bassboost")]
        [Summary("Boosts the bass of the currently playing track.")]
        public async Task BassBoostCmd([Summary("The multiplier on the bass.")]double bass)
        {
            try
            {
                var user = Context.User;
                var song = _ms.GetQueue(Context).First();

                if (user.IsQueuer(song) || (user as SocketGuildUser).GuildPermissions.DeafenMembers)
                {
                    if (bass < 0 || bass > 8) bass = 2;

                    var bassVal = (.25d * bass) - .25d;

                    List<EqualizerBand> eBands = new List<EqualizerBand>();
                    for (int i = 0; i < 5; i++)
                        eBands.Add(new EqualizerBand { Band = (ushort)i, Gain = bassVal });
                    for (int i = 5; i < 15; i++)
                        eBands.Add(new EqualizerBand { Band = (ushort)i, Gain = 0 });

                    await _ms.EqualizeAsync(Context, eBands);

                    await ReplyAsync($"Set bass to **{bass}x**!");
                }
            }
            catch (Exception e)
            {
                await ReplyAsync(embed: (await _misc.GenerateErrorMessage(e)).Build());
            }
        }
        [Command("treble")]
        [Alias("trebleboost")]
        [Summary("Boosts the treble of the currently playing track.")]
        public async Task TrebleBoostCmd([Summary("The multiplier on the treble.")]double treble)
        {
            try
            {
                var user = Context.User;
                var song = _ms.GetQueue(Context).First();

                if (user.IsQueuer(song) || (user as SocketGuildUser).GuildPermissions.DeafenMembers)
                {
                    if (treble < 0 || treble > 8) treble = 2;
                    
                    var trebleVal = (.25d * treble) - .25d;

                    List<EqualizerBand> eBands = new List<EqualizerBand>();
                    for (int i = 0; i < 10; i++)
                        eBands.Add(new EqualizerBand { Band = (ushort)i, Gain = 0 });
                    for (int i = 10; i < 15; i++)
                        eBands.Add(new EqualizerBand { Band = (ushort)i, Gain = trebleVal });

                    await _ms.EqualizeAsync(Context, eBands);

                    await ReplyAsync($"Set treble to **{treble}x**!");
                }
            }
            catch (Exception e)
            {
                await ReplyAsync(embed: (await _misc.GenerateErrorMessage(e)).Build());
            }
        }

        [Command("reset")]
        [Summary("Resets the equalizer on the currently playing track.")]
        public async Task ResetCmd()
        {
            try
            {
                var user = Context.User;
                var song = _ms.GetQueue(Context).First();

                if (!(user as SocketGuildUser).GuildPermissions.DeafenMembers || !user.IsQueuer(song)) return;

                List<EqualizerBand> eBands = new List<EqualizerBand>();

                for (int i = 0; i < 15; i++)
                    eBands.Add(new EqualizerBand { Band = (ushort)i, Gain = 0 });

                await _ms.EqualizeAsync(Context, eBands);
            }
            catch (Exception e)
            {
                await ReplyAsync(embed: (await _misc.GenerateErrorMessage(e)).Build());
            }
        }
        */

        [Command("skip")]
        [Summary("Votes to skip the current track, or skips it entirely if you queued it.")]
        public async Task SkipCmd()
        {
            try
            {
                await _ms.SkipAsync(Context);
            }
            catch (Exception e)
            {
                await ReplyAsync(embed: (await _misc.GenerateErrorMessage(e)).Build());
            }
        }

        [Command("skips")]
        [Summary("Displays the amount of skips the currently playing track has.")]
        public async Task SkipsCmd()
        {
            try
            {
                var skips = _ms.GetSkips(Context);
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
                await ReplyAsync(embed: (await _misc.GenerateErrorMessage(e)).Build());
            }
        }
    }
    
}
