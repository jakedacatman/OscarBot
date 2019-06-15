using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Discord;
using Discord.Audio;
using Discord.WebSocket;
using OscarBot.Classes;
using YoutubeExplode;
using YoutubeExplode.Models.MediaStreams;

namespace OscarBot.Services
{
    public class AudioService
    {
        private readonly DiscordShardedClient _client;
        private readonly MiscService _misc;

        private List<AudioPlayer> players = new List<AudioPlayer>();

        public int PlayingPlayers { get { return players.Where(x => x.IsPlaying).Count(); } }
        public long MemoryUsage { get { return players.Where(x => x.IsPlaying).Select(x => x.MemoryUsage).Sum(); } }

        private bool _isRunning = false;

        public AudioService(DiscordShardedClient client, MiscService misc)
        {
            _client = client;
            _misc = misc;
            _client.ShardReady += ShardReady;
        }

        private Task ShardReady(DiscordSocketClient cl)
        {
            if (!_isRunning)
            {
                Console.WriteLine($"{DateTime.Now,19} [{"Info",8}] AudioService: Starting AudioService.");
                _client.ShardReady -= ShardReady;
                Task.Run(WaitForTrackEnd);
                _isRunning = true;
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// player, reason
        /// </summary>
        public event Func<AudioPlayer, string, Task> TrackEnd;

        public AudioPlayer GetPlayer(ulong guildId) => players.Where(x => x.GuildId == guildId).FirstOrDefault();

        public async Task<AudioPlayer> JoinAsync(IVoiceChannel voiceChannel, ISocketMessageChannel textChannel)
        {
            var client = await voiceChannel.ConnectAsync(true);
            var player = new AudioPlayer(voiceChannel.GuildId, textChannel, voiceChannel, client, this, _client);
            players.Add(player);
            return player;
        }

        public async Task LeaveAsync(IVoiceChannel voiceChannel)
        {
            await voiceChannel.DisconnectAsync();
            players.RemoveAll(x => x.GuildId == voiceChannel.GuildId);
        }

        private async Task WaitForTrackEnd()
        {
            while (true)
            {
                foreach (var p in players.Where(x => x._didPlay))
                    TrackEnd?.Invoke(p, "finished");
                await Task.Delay(500);
            }
        }
        /*
        private string GetAudioUrl(string url)
        {
            var ytdl = new ProcessStartInfo
            {
                FileName = "youtube-dl.exe",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                Arguments = $"-g \"{url}\""
            };

            List<string> received = new List<string>();

            var proc = Process.Start(ytdl);
            proc.OutputDataReceived += (object sender, DataReceivedEventArgs args) => received.Add(args.Data);
            proc.BeginOutputReadLine();
            while (!proc.HasExited)
            {
                // wait
            }

            var query = received.Where(x => x.Contains("mime=audio"));
            var toReturn = query.Any() ? query.First() : received.LastOrDefault();
            return toReturn;
        }*/

        public async Task<AudioStreamInfo> GetStreamInfo(string id)
        {
            AudioStreamInfo result = null;
            await Task.Run(async () =>
            {
            var cl = new YoutubeClient();
                result = (await cl.GetVideoMediaStreamInfosAsync(id)).Audio.OrderByDescending(x => x.Bitrate).First();
            });
            return result;
        }
    }
}
