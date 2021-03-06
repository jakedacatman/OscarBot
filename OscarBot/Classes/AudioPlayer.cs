﻿using System;
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
using System.IO;

namespace OscarBot.Classes
{
    public class AudioPlayer
    {
        public ulong GuildId { get; }
        public ISocketMessageChannel TextChannel { get;  }
        public IVoiceChannel VoiceChannel { get; }
        public uint Volume { get; private set; }
        public bool IsPlaying { get; private set; }
        public bool IsPaused { get; private set; }
        public Song CurrentSong { get; private set; }
        public TimeSpan CurrentPosition { get; private set; }
        public long MemoryUsage { get; private set; }

        internal bool _didPlay = false;
        private bool _isskipped = false;
        private readonly IAudioClient _audioClient;
        private readonly DiscordShardedClient _client;
        private const int bufferSize = 3840;
        private long wasSent = 0;
        private readonly AudioService _as;
        private Process _ffmpeg;

        internal AudioPlayer(ulong guildId, ISocketMessageChannel textChannel, IVoiceChannel voiceChannel, IAudioClient audioClient, AudioService a_s, DiscordShardedClient client)
        {
            GuildId = guildId;
            TextChannel = textChannel;
            VoiceChannel = voiceChannel;
            Volume = 100;
            _audioClient = audioClient;
            _client = client;
            _as = a_s;
            MemoryUsage = 0;
        }

        private Process StartFFMpeg(string url)
        {
            var ffmpeg = new ProcessStartInfo
            {
                FileName = "ffmpeg.exe",
                Arguments = $"-i \"{url}\" -ac 2 -nostdin -ar 48000 -map 0:a:0 -b:a 512k -loglevel verbose -f s16le pipe:1",
                RedirectStandardOutput = true,
                RedirectStandardError = false,
                CreateNoWindow = true,
                UseShellExecute = false
            };
            Console.WriteLine($"starting ffmpeg in guild {GuildId}");
            return Process.Start(ffmpeg);
        }

        public async Task PlayAsync(Song s)
        {
            try
            {
                Reset(true, false, false, s);

                _ffmpeg = StartFFMpeg(s.AudioURL);

                var buffer = new byte[bufferSize];

                var fromffmpeg = new BufferedStream(_ffmpeg.StandardOutput.BaseStream, bufferSize);
                var todiscord = _audioClient.CreatePCMStream(AudioApplication.Music);

                var promise = new TaskCompletionSource<bool>();
                var promiseTimeout = Task.Delay(10000);

                void AwaitFFMpegOutput(object sender, DataReceivedEventArgs e)
                {
                    promise.SetResult(true);
                }

                _ffmpeg.OutputDataReceived += AwaitFFMpegOutput;
                await Task.WhenAny(promise.Task, promiseTimeout);
                _ffmpeg.OutputDataReceived -= AwaitFFMpegOutput;

                Console.WriteLine($"ffmpeg started in guild {GuildId}");

                try
                {
                    while (!_isskipped)
                    {
                        MemoryUsage = _ffmpeg.PeakWorkingSet64;
                        var wasRead = await fromffmpeg.ReadAsync(buffer, 0, bufferSize);

                        if (wasRead == 0) break;

                        while (IsPaused)
                            await Task.Delay(10);

                        buffer = SetVolume(buffer, Volume);

                        await todiscord.WriteAsync(buffer, 0, wasRead);

                        wasSent += buffer.Length;
                        CurrentPosition = TimeSpan.FromSeconds(wasSent / (bufferSize * 50));
                    }
                }
                catch { }
                finally
                {
                    var timeout = Task.Delay(2000);
                    await Task.WhenAny(todiscord.FlushAsync(), timeout);
                }

                Reset(false, true, true);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Reset(false, true, true);
            }
        }

        private void Reset(bool isPlaying, bool didPlay, bool dispose = false, Song s = null)
        {
            MemoryUsage = 0;
            Volume = 100;
            CurrentPosition = TimeSpan.FromMilliseconds(0);
            IsPlaying = isPlaying;
            CurrentSong = s;
            _didPlay = didPlay;
            wasSent = 0;
            if (dispose)
                _ffmpeg.Dispose();

            _didPlay = false;
        }

        //modified from https://github.com/tigertub/nadeendko/blob/423e219be1f975101cc954e22dd07416d21b4002/NadekoBot/Modules/Music/Classes/Song.cs
        private unsafe byte[] SetVolume(byte[] audioSamples, uint volume)
        {
            var vol = volume / 100d;
            if (vol > 1000) vol = 1d;
            
            int volumeFixed = (int)Math.Round(vol * 65536d);

            int count = (int)Math.Round(audioSamples.Length / 2d);

            fixed (byte* srcBytes = audioSamples)
            {
                short* src = (short*)srcBytes;

                for (int i = count; i != 0; i--, src++)
                    *src = (short)(((*src) * volumeFixed) >> 16);
            }

            return audioSamples;
        }


        public void SetVolume(uint volume)
        {
            if (IsPlaying)
                Volume = volume;
        }

        public void Skip()
        {
            _isskipped = true;
            _didPlay = true;
        }
    }
}
