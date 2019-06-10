﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Discord.Addons.Interactive;
using OscarBot.Services;
using System.Net;
using System.IO;

namespace OscarBot.Modules
{
    [Name("Image Manipulation")]
    public class ImageModule : InteractiveBase<ShardedCommandContext>
    {
        private readonly ImageService _img;
        private readonly MiscService _misc;

        public ImageModule(ImageService img, MiscService misc)
        {
            _img = img;
            _misc = misc;
        }

        [Command("compress")]
        [Alias("comp")]
        [Summary("Compresses an image.")]
        public async Task CompCmd([Summary("The user whose avatar will be compressed.")]SocketGuildUser user = null, [Summary("The percentage quality desired.")]long quality = 50)
        {
            try
            {
                if (quality < 0 || quality > 100) quality = 50;
                
                string url;
                if (user == null)
                    url = Context.User.GetAvatarUrl(ImageFormat.Auto, 512);
                else url = user.GetAvatarUrl(ImageFormat.Auto, 512);
                using (var c = new WebClient())
                {
                    byte[] s = c.DownloadData(new Uri(url));
                    Stream fs = new MemoryStream(s);
                    System.Drawing.Image i = System.Drawing.Image.FromStream(fs);
                    fs.Close();
                    var path = _img.Compress(new System.Drawing.Bitmap(i), "Jpeg", quality);
                    await Context.Channel.SendFileAsync(path);
                    File.Delete(path);
                }
            }
            catch (Exception e)
            {
                await ReplyAsync(embed: _misc.GenerateErrorMessage(e).Build());
            }
        }
        [Command("compress")]
        [Alias("comp")]
        [Summary("Compresses an image.")]
        public async Task CompCmd([Summary("The URL of the image in question.")]string url = null, [Summary("The shortened form of the format.")]string format = "jpg", [Summary("The percentage quality desired.")]long quality = 50)
        {
            try
            {
                if (quality < 0 || quality > 100) quality = 50;
                if (url == null || !Uri.TryCreate(url.Trim('<', '>'), UriKind.Absolute, out Uri result))
                {
                    var att = Context.Message.Attachments;
                    if (att.Count == 0)
                    {
                        await ReplyAsync("Enter a URL, or attach an image.");
                        return;

                    }
                    url = att.First().Url;
                    
                    if (url == null || !Uri.TryCreate(url.Trim('<', '>'), UriKind.Absolute, out result))
                    {
                        await ReplyAsync("Enter a URL, or attach an image.");
                        return;
                    }
                }
                using (var c = new WebClient())
                {
                    if (result == null)
                    {
                        await ReplyAsync("Make sure the URL is valid.");
                        return;
                    }
                    byte[] s = c.DownloadData(result);
                    Stream fs = new MemoryStream(s);
                    System.Drawing.Image i = System.Drawing.Image.FromStream(fs);
                    fs.Close();
                    var path = _img.Compress(new System.Drawing.Bitmap(i), format, quality);
                    await Context.Channel.SendFileAsync(path);
                    File.Delete(path);
                }
            }
            catch (Exception e)
            {
                await ReplyAsync(embed: _misc.GenerateErrorMessage(e).Build());
            }
        }
        [Command("compress")]
        [Alias("comp")]
        [Summary("Compresses an image.")]
        public async Task CompCmd([Summary("The percentage quality desired.")]long quality = 50, [Summary("The shortened form of the format.")]string format = "jpg")
        {
            try
            {
                if (quality < 0 || quality > 100) quality = 50;
                    string url = Context.Message.Attachments.Any() ? Context.Message.Attachments.First().Url.Trim('<', '>') : Context.User.GetAvatarUrl(ImageFormat.Auto, 512);
                if (url == null || !Uri.IsWellFormedUriString(url, UriKind.Absolute))
                {
                    await ReplyAsync("Attach an image.");
                    return;
                }

                using (var c = new WebClient())
                {
                    Uri.TryCreate(url, UriKind.Absolute, out Uri result);
                    if (result == null)
                    {
                        await ReplyAsync("Make sure the URL is valid.");
                        return;
                    }
                    byte[] s = c.DownloadData(result);
                    Stream fs = new MemoryStream(s);
                    System.Drawing.Image i = System.Drawing.Image.FromStream(fs);
                    fs.Close();
                    var path = _img.Compress(new System.Drawing.Bitmap(i), format, quality);
                    await Context.Channel.SendFileAsync(path);
                    File.Delete(path);
                }
            }
            catch (Exception e)
            {
                await ReplyAsync(embed: _misc.GenerateErrorMessage(e).Build());
            }
        }

        [Command("avatar")]
        [Alias("av")]
        [Summary("Fetches the avatar of a user.")]
        public async Task AvatarCmd([Summary("The user whose avatar should be fetched.")]SocketUser user = null)
        {
            if (user == null) user = Context.User;
            if (Context.Guild.Users.Where(x => x.ToString() == user.ToString()).Count() == 0) return;
            await ReplyAsync(embed: new EmbedBuilder().WithImageUrl(user.GetAvatarUrl(ImageFormat.Auto, 512)).WithColor(_misc.RandomColor()).Build());
        }

        [Command("randclr")]
        [Alias("randomcolor")]
        [Summary("Makes each color in an image a random color.")]
        public async Task RandColorCmd([Summary("The user whose avatar's colors will be replaced with random ones.")]SocketGuildUser user = null, [Summary("The amount of similarity between colors where similar colors will become the same random color.")]int tolerance = 0)
        {
            try
            {
                string url;
                if (user == null)
                    url = Context.User.GetAvatarUrl(ImageFormat.Auto, 512);
                else url = user.GetAvatarUrl(ImageFormat.Auto, 512);
                using (var c = new WebClient())
                {
                    byte[] s = c.DownloadData(new Uri(url));
                    Stream fs = new MemoryStream(s);
                    System.Drawing.Image i = System.Drawing.Image.FromStream(fs);
                    fs.Close();
                    var path = _img.RandomColor(new System.Drawing.Bitmap(i), "Jpeg", 100, tolerance);
                    await Context.Channel.SendFileAsync(path);
                    File.Delete(path);
                }
            }
            catch (Exception e)
            {
                await ReplyAsync(embed: _misc.GenerateErrorMessage(e).Build());
            }
        }
        [Command("randclr")]
        [Alias("randomcolor")]
        [Summary("Makes each color in an image a random color.")]
        public async Task RandColorCmd([Summary("The URL of the image in question.")]string url = null, [Summary("The shortened form of the format.")]string format = "jpg", [Summary("The amount of similarity between colors where similar colors will become the same random color.")]int tolerance = 0)
        {
            try
            {
                if (url == null || !Uri.TryCreate(url.Trim('<', '>'), UriKind.Absolute, out Uri result))
                {
                    var att = Context.Message.Attachments;
                    if (att.Count == 0)
                    {
                        await ReplyAsync("Enter a URL, or attach an image.");
                        return;

                    }
                    url = att.First().Url;

                    if (url == null || !Uri.TryCreate(url.Trim('<', '>'), UriKind.Absolute, out result))
                    {
                        await ReplyAsync("Enter a URL, or attach an image.");
                        return;
                    }
                }
                using (var c = new WebClient())
                {
                    if (result == null)
                    {
                        await ReplyAsync("Make sure the URL is valid.");
                        return;
                    }
                    byte[] s = c.DownloadData(result);
                    Stream fs = new MemoryStream(s);
                    System.Drawing.Image i = System.Drawing.Image.FromStream(fs);
                    fs.Close();
                    var path = _img.RandomColor(new System.Drawing.Bitmap(i), format, 100, tolerance);
                    await Context.Channel.SendFileAsync(path);
                    File.Delete(path);
                }
            }
            catch (Exception e)
            {
                await ReplyAsync(embed: _misc.GenerateErrorMessage(e).Build());
            }
        }
        [Command("randclr")]
        [Alias("randomcolor")]
        [Summary("Makes each color in an image a random color.")]
        public async Task RandColorCmd([Summary("The shortened form of the format.")]string format = "jpg", [Summary("The amount of similarity between colors where similar colors will become the same random color.")]int tolerance = 0)
        {
            try
            {
                string url = Context.Message.Attachments.Any() ? Context.Message.Attachments.First().Url.Trim('<', '>') : Context.User.GetAvatarUrl(ImageFormat.Auto, 512);
                if (url == null || !Uri.IsWellFormedUriString(url, UriKind.Absolute))
                {
                    await ReplyAsync("Attach an image.");
                    return;
                }

                using (var c = new WebClient())
                {
                    Uri.TryCreate(url, UriKind.Absolute, out Uri result);
                    if (result == null)
                    {
                        await ReplyAsync("Make sure the URL is valid.");
                        return;
                    }
                    byte[] s = c.DownloadData(result);
                    Stream fs = new MemoryStream(s);
                    System.Drawing.Image i = System.Drawing.Image.FromStream(fs);
                    fs.Close();
                    var path = _img.RandomColor(new System.Drawing.Bitmap(i), format, 100, tolerance);
                    await Context.Channel.SendFileAsync(path);
                    File.Delete(path);
                }
            }
            catch (Exception e)
            {
                await ReplyAsync(embed: _misc.GenerateErrorMessage(e).Build());
            }
        }
    }
}
