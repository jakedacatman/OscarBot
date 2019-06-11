using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Discord.Addons.Interactive;
using Discord.Net;
using OscarBot.Classes;
using OscarBot.Services;

namespace OscarBot.Modules
{
    [Name("Moderation")]
    public class ModeratorModule : InteractiveBase<ShardedCommandContext>
    {
        private readonly DbService _db;
        private readonly ModerationService _ms;
        private readonly MiscService _misc;

        public ModeratorModule(DbService db, ModerationService ms, MiscService misc)
        {
            _db = db;
            _ms = ms;
            _misc = misc;
        }

        [Command("mute")]
        [Summary("Prevents a user from sending messages, adding reactions, or speaking.")]
        [RequireUserPermission(GuildPermission.MuteMembers)]
        [RequireBotPermission(GuildPermission.MuteMembers | GuildPermission.ManageGuild)]
        public async Task MuteCmd([Summary("The user to mute.")] SocketGuildUser user, [Summary("The timespan to mute the user for.")] TimeSpan timespan, [Summary("The reason to mute the user; may be blank."), Remainder] string reason = null)
        {
            try
            {
                if (await _ms.TryMuteUserAsync(Context.Guild, Context.User as SocketGuildUser, user, timespan, reason))
                    await ReplyAsync("Successfully muted user.");
                else
                    await ReplyAsync("Failed to mute user. (are they already muted?)");
            }
            catch (Exception e)
            {
                await ReplyAsync(embed: (await _misc.GenerateErrorMessage(e)).Build());
            }
        }

        [Command("unmute")]
        [RequireBotPermission(GuildPermission.ManageRoles)]
        [RequireUserPermission(GuildPermission.MuteMembers)]
        [Summary("Unmutes a user.")]
        public async Task UnmuteCommand([Summary("The user to unmute."), Remainder]SocketGuildUser user)
        {
            try
            {
                if (await _ms.TryUnmuteUserAsync(Context.Guild, user))
                    await ReplyAsync("Successfully unmuted user.");
                else
                    await ReplyAsync("Failed to unmute user. (are they already unmuted?)");
            }

            catch (Exception e)
            {
                await ReplyAsync(embed: (await _misc.GenerateErrorMessage(e)).Build());
            }
        }

        [Command("modaction")]
        [Alias("moderationaction")]
        [Summary("View the currently-applying moderation action of a user.")]
        public async Task GetModAction(SocketGuildUser user)
        {
            try
            {
                var sb = new StringBuilder();
                foreach (var action in (await _ms.GetModerationActionsAsync(Context.Guild.Id)).Actions)
                    sb.Append($"{action.Type.ToString()}\n");

                var embed = new EmbedBuilder()
                    .WithTitle($"Current moderation action for {user}:")
                    .WithColor(_misc.RandomColor())
                    .WithThumbnailUrl(user.GetAvatarUrl(size: 512))
                    .WithCurrentTimestamp()
                    .WithDescription(sb.ToString());

                await ReplyAsync(embed: embed.Build());
            }
            catch (Exception e)
            {
                await ReplyAsync(embed: (await _misc.GenerateErrorMessage(e)).Build());
            }
        }

        private async Task ModifyUserInChannelAsync(IUser user, Action<OverwritePermissions> perms, string reason = null, SocketGuildChannel channel = null)
        {
            if (channel == null) channel = Context.Channel as SocketGuildChannel;

            var overwrite = channel.GetPermissionOverwrite(user) ?? OverwritePermissions.InheritAll;
            perms(overwrite);

            try
            {
                RequestOptions options = RequestOptions.Default;

                if (reason != null) options.AuditLogReason = reason;

                await (channel as SocketTextChannel).AddPermissionOverwriteAsync(user, overwrite, options);

                await ReplyAsync("Successfully modified user.");
            }
            catch (HttpException e) when (e.DiscordCode.GetValueOrDefault() == 50013)
            {
                var embed = (await _misc.GenerateErrorMessage(e));
                embed.Description = $"{user.Mention} is above me in the hierarchy, so I cannot complete the requested action.";

                await ReplyAsync(embed: embed.Build());
            }
        }
    }
}
