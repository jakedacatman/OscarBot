using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;

namespace OscarBot.Services
{
    public class MiscService
    {
        private readonly string[] errorMessages = new string[]
        {
            "Whoops!",
            "Sorry!",
            "An error has occured.",
            "well frick",
            "Okay, this is not epic",
            "Uh-oh!",
            "Something went wrong.",
            "Oh snap!",
            "Oops!",
            "Darn...",
            "I can't believe you've done this.",
            "SAD!",
            "Thank you Discord user, very cool",
            "bruh",
            "HTTP 418 I'm a teapot",
            "I don't feel so good...",
            "On your left!",
            "[insert funny phrase]"
        };

        public EmbedBuilder GenerateErrorMessage(Exception e)
        {
            Random r = new Random();
            Console.WriteLine(e.ToString());

            EmbedBuilder embed = new EmbedBuilder()
                .WithColor(RandomColor())
                .WithCurrentTimestamp()
                .WithFooter(e.GetType().ToString())
                .WithDescription($"This command has thrown an exception. Here is its message:\n**{e.Message}**\nStack trace:\n```{e.StackTrace}```")
                .WithTitle(errorMessages[r.Next(errorMessages.Length)]);

            return embed;
        }

        public Color RandomColor()
        {
            Random r = new Random();
            uint clr = Convert.ToUInt32(r.Next(0, 0xFFFFFF));
            return new Color(clr);
        }
    }
}
