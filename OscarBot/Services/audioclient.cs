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

namespace OscarBot.Services
{
    public class AudioClient
    {
        private readonly DiscordShardedClient _client;
        private readonly MiscService _misc;

        private List<AudioPlayer> players = new List<AudioPlayer>();

        public AudioClient(DiscordShardedClient client, MiscService misc)
        {
            _client = client;
            _misc = misc;
        }

        public AudioPlayer GetPlayer(ulong guildId) => players.Where(x => x.GuildId == guildId).FirstOrDefault();

        public async Task<AudioPlayer> JoinAsync(IVoiceChannel voiceChannel, ISocketMessageChannel textChannel)
        {
            var client = await voiceChannel.ConnectAsync(true);
            var player = new AudioPlayer(voiceChannel.GuildId, textChannel, voiceChannel, client, this);
            players.Add(player);
            return player;
        }

        public async Task LeaveAsync(IVoiceChannel voiceChannel)
        {
            await voiceChannel.DisconnectAsync();
            players.RemoveAll(x => x.GuildId == voiceChannel.GuildId);
        }
        
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
        }

        public async Task<string> GetAudioUrlAsync(string url)
        {
            string result = null;
            await Task.Run(() => result = GetAudioUrl(url));
            return result;
        }
    }
}
