using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OscarBot.Classes;
using Discord.WebSocket;
using Discord;

namespace OscarBot.Services
{
    public class MusicService
    {
        private readonly DiscordShardedClient _client;
        private readonly MiscService _misc;
        private readonly EntityContext _db;

        public MusicService(DiscordShardedClient client, MiscService misc, EntityContext db)
        {
            _client = client;
            _misc = misc;
            _db = db;
        }

        public async Task<ICollection<Song>> GetGuildQueue(ulong id)
        {
            var queues = _db.Queues;
            var query = queues.Where(x => x.GuildId == id);
            ICollection<Song> queue;
            if (query.Count() == 0)
            {
                queue = new List<Song>();
                queues.Add(new GuildQueue { Queue = queue, GuildId = id, Skipped = new List<Skip>() });
            }
            else
                queue = query.Single().Queue;
            await _db.SaveChangesAsync();
            return queue;
        }

        public async Task<List<Skip>> GetSkips(ulong id)
        {
            var queues = _db.Queues;
            var query = queues.Where(x => x.GuildId == id);
            List<Skip> skips;
            if (query.Count() == 0)
            {
                skips = new List<Skip>();
                queues.Add(new GuildQueue { Queue = new List<Song>(), GuildId = id, Skipped = skips });
            }
            else
                skips = (List<Skip>)query.Single().Skipped;
            await _db.SaveChangesAsync();
            return skips;
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
