using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Audio;
using Discord.WebSocket;
using System.Diagnostics;
using OscarBot.Services;

namespace OscarBot.Classes
{
    public class AudioPlayer
    {
        public ulong GuildId { get; }
        public ISocketMessageChannel TextChannel { get;  }
        public IVoiceChannel VoiceChannel { get; }
        public uint Volume { get; private set; }
        public bool IsPlaying { get; private set; }
        public Song CurrentSong { get; private set; }

        private readonly IAudioClient _client;
        private CancellationTokenSource cts;
        private readonly AudioClient _ac;

        internal AudioPlayer(ulong guildId, ISocketMessageChannel textChannel, IVoiceChannel voiceChannel, IAudioClient client, AudioClient ac)
        {
            GuildId = guildId;
            TextChannel = textChannel;
            VoiceChannel = voiceChannel;
            Volume = 100;
            _client = client;
            _ac = ac;
        }

        /// <summary>
        /// player, song, reason
        /// </summary>
        public event Func<AudioPlayer, Song, string, Task> TrackEnd;

        private Process StartFFMpeg(string url)
        {
            var ffmpeg = new ProcessStartInfo
            {
                FileName = "ffmpeg.exe",
                Arguments = $"-i \"{url}\" -codec copy -ac 2 -nostdin -ar 48000 -map 0:a:0 -b:a 512k -reconnect 1 -loglevel verbose -reconnect_streamed 1 -reconnect_delay_max 5 -f s16le pipe:1",
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            return Process.Start(ffmpeg);
        }

        public async Task PlayAsync(Song s)
        {
            try
            {
                Volume = 100;
                IsPlaying = true;
                CurrentSong = s;

                var ffmpeg = StartFFMpeg(s.AudioURL);

                var output = ffmpeg.StandardOutput.BaseStream;
                var input = _client.CreatePCMStream(AudioApplication.Mixed);

                await output.CopyToAsync(input, 81920, cts.Token);

                await input.FlushAsync();

                TrackEnd?.Invoke(this, s, "finished");
            }
            catch
            {
                IsPlaying = false;
            }
        }

        public void SetVolume(uint volume)
        {
            Volume = volume;
        }

        public void Skip()
        {
            IsPlaying = false;
            cts.Cancel();
            cts = new CancellationTokenSource();
        }
    }
}
