using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using OscarBot.Classes;
using Discord;
using Discord.WebSocket;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace OscarBot.Services
{
    public class ModerationService
    {
        private readonly IServiceProvider _services;
        private readonly DiscordShardedClient _client;
        private bool UnpunishIsRunning = false;
        public List<ModerationActionCollection> _actions = new List<ModerationActionCollection>();

        public ModerationService(IServiceProvider services, DiscordShardedClient client)
        {
            _services = services;
            _client = client;
            _client.ShardReady += ShardReady;
        }
        
        private Task ShardReady(DiscordSocketClient cl)
        {
            if (!UnpunishIsRunning)
            {
                _client.ShardReady -= ShardReady;
                Task.Run(StartAsync);
                UnpunishIsRunning = true;
            }

            return Task.CompletedTask;
        }

        private async Task UpdateActions()
        {
            foreach (var g in _client.Guilds)
            {
                var actions = await GetModerationActionsAsync(g.Id);
                if (actions.Any()) _actions.Add(actions);
            }
        }

        public async Task StartAsync()
        {
            try
            {
                if (!UnpunishIsRunning) UnpunishIsRunning = true;
                Console.WriteLine($"{DateTime.Now,19} [{"Info",8}] ModeratorService: Starting ModeratorService.");
                while (UnpunishIsRunning)
                {
                    await Task.Delay(1000);

                    await UpdateActions();
                    if (_actions.Where(x => x.Any()).Any())
                    {
                        Console.WriteLine($"{DateTime.Now,19} [{"Verbose",8}] ModeratorService: Attempting to revert actions.");
                        foreach (var action in _actions.SelectMany(x => x.Where(y => DateTime.UtcNow >= y.ReverseAfter)))
                        {
                            var guild = _client.GetGuild(action.GuildId);
                            var user = guild.GetUser(action.UserId);

                            if (guild == null || user == null) break;

                            switch (action.Type)
                            {
                                case ModerationAction.ActionType.Mute:
                                    IRole role;
                                    if (guild.Roles.Any(x => x.Name == "Muted"))
                                        role = guild.Roles.FirstOrDefault(x => x.Name == "Muted");
                                    else break;

                                    if (role == null) break;

                                    await TryUnmuteUserAsync(guild, user);

                                    Console.WriteLine($"{DateTime.Now,19} [{"Verbose",8}] PunishService: Undid moderation action for {user}.");

                                    await RemoveModerationActionAsync(action);
                                    break;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"{ToString()} has thrown an exception: {e.Message}\nStack trace: \n{e.StackTrace}");
                //await StartAsync();
            }
        }


        public async Task<ModerationActionCollection> GetModerationActionsAsync(ulong guildId)
        {
            using (var scope = _services.CreateScope())
            {
                var _db = scope.ServiceProvider.GetRequiredService<EntityContext>();

                var c = _db.ModerationActions;
                var query = c.Include(x => x).Where(x => x.GuildId == guildId);
                ModerationActionCollection actions;
                if (!query.Any())
                {
                    actions = new ModerationActionCollection { GuildId = guildId };
                    c.Add(actions);
                }
                else
                    actions = query.Single();
                await _db.SaveChangesAsync();
                return actions;
            }
        }

        public async Task<int> AddModerationActionAsync(ModerationAction action)
        {
            using (var scope = _services.CreateScope())
            {
                var _db = scope.ServiceProvider.GetRequiredService<EntityContext>();

                var actions = _db.ModerationActions;
                var query = actions.Include(x => x).Where(x => x.GuildId == action.GuildId);
                ModerationActionCollection list;
                if (!query.Any())
                {
                    list = new ModerationActionCollection { GuildId = action.GuildId };
                    actions.Add(list);
                }
                else
                    list = query.Single();

                list.Add(action);
                return await _db.SaveChangesAsync();
            }
        }
        public async Task<int> RemoveModerationActionAsync(ModerationAction action)
        {
            using (var scope = _services.CreateScope())
            {
                var _db = scope.ServiceProvider.GetRequiredService<EntityContext>();

                var actions = _db.ModerationActions;
                var query = actions.Where(x => x.GuildId == action.GuildId);
                ModerationActionCollection list;
                if (!query.Any())
                {
                    list = new ModerationActionCollection { GuildId = action.GuildId };
                    actions.Add(list);
                }
                else
                    list = query.Single();

                if (list.Contains(action))
                    list.Remove(action);
                return await _db.SaveChangesAsync();
            }
        }
        public async Task<int> RemoveModerationActionAsync(SocketGuildUser user, ModerationAction.ActionType type)
        {
            using (var scope = _services.CreateScope())
            {
                var _db = scope.ServiceProvider.GetRequiredService<EntityContext>();

                var actions = _db.ModerationActions;
                var query = actions.Where(x => x.GuildId == user.Guild.Id);
                ModerationActionCollection list;
                if (!query.Any())
                {
                    list = new ModerationActionCollection { GuildId = user.Guild.Id };
                    actions.Add(list);
                }
                else
                {
                    list = query.Single();
                }

                list.RemoveAll(x => x.UserId == user.Id && x.Type == type);

                return await _db.SaveChangesAsync();
            }
        }

        public async Task<bool> TryMuteUserAsync(SocketGuild guild, SocketGuildUser moderator, SocketGuildUser user, TimeSpan timeToRevert, string reason = null)
        {
            try
            {
                IRole role;

                if (guild.Roles.Any(x => x.Name == "Muted"))
                    role = guild.Roles.First(x => x.Name == "Muted");
                else
                {
                    OverwritePermissions Permissions = new OverwritePermissions(addReactions: PermValue.Deny, sendMessages: PermValue.Deny, attachFiles: PermValue.Deny, useExternalEmojis: PermValue.Deny, speak: PermValue.Deny);

                    role = await guild.CreateRoleAsync("Muted", GuildPermissions.None, Color.Default);

                    await role.ModifyAsync(x => x.Position = guild.GetUser(_client.CurrentUser.Id).Roles.OrderBy(y => y.Position).Last().Position);

                    foreach (var channel in (guild as SocketGuild).TextChannels)
                        if (!channel.PermissionOverwrites.Select(x => x.Permissions).Contains(Permissions))
                            await channel.AddPermissionOverwriteAsync(role, Permissions);
                }

                if (user.Roles.Contains(role)) return false;

                await user.AddRoleAsync(role);
                await user.ModifyAsync(x => x.Mute = true);

                DateTime reverseAfter;
                if (timeToRevert == TimeSpan.MaxValue) reverseAfter = DateTime.MaxValue;
                else reverseAfter = DateTime.UtcNow.Add(timeToRevert);

                await AddModerationActionAsync(new ModerationAction
                {
                    Type = ModerationAction.ActionType.Mute,
                    GuildId = guild.Id,
                    UserId = user.Id,
                    ModeratorId = moderator.Id,
                    ReverseAfter = reverseAfter,
                    Reason = reason ?? "no reason given"
                });

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }
        }

        public async Task<bool> TryUnmuteUserAsync(SocketGuild guild, SocketGuildUser user)
        {
            try
            {
                SocketRole role;

                if (guild.Roles.Any(x => x.Name == "Muted"))
                    role = guild.Roles.FirstOrDefault(x => x.Name == "Muted");
                else return false;

                if (!user.Roles.Contains(role)) return false;

                await user.RemoveRoleAsync(role);
                await user.ModifyAsync(x => x.Mute = false);

                await RemoveModerationActionAsync(user, ModerationAction.ActionType.Mute);

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }
        }
    }
}

