﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using OscarBot.Classes;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using System.Diagnostics;
using Newtonsoft.Json;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Discord.Commands;

namespace OscarBot.Services
{
    public class MiscService
    {
        private readonly DiscordShardedClient _client;
        private IServiceProvider _services;
        private readonly Random _random;
        private List<int> ids = new List<int>();

        public MiscService(DiscordShardedClient client, IServiceProvider services, Random random)
        {
            _client = client;
            _services = services;
            _random = random;
        }

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

        public async Task<EmbedBuilder> GenerateErrorMessage(Exception e)
        {
            Console.WriteLine(e.ToString());

            string description = "This command has thrown an exception. Here is ";

            if (e.Message.Length < 1000)
                description += $"its message:\n**{e.Message}**";
            else
                description += $"a [link]({await UploadToBisogaAsync(e.Message)}) to its message.";
            description += "\nStack trace:\n";
            if (e.StackTrace.Length < 1000)
                description += $"```{e.StackTrace}```";
            else
                description += $"[here]({await UploadToBisogaAsync(e.StackTrace)})";

            EmbedBuilder embed = new EmbedBuilder()
                .WithColor(RandomColor())
                .WithCurrentTimestamp()
                .WithFooter(e.GetType().ToString())
                .WithDescription(description)
                .WithTitle(errorMessages[_random.Next(errorMessages.Length)]);

            return embed;
        }

        public Color RandomColor()
        {
            Random r = new Random();
            uint clr = Convert.ToUInt32(r.Next(0, 0xFFFFFF));
            return new Color(clr);
        }

        public async Task<EmbedBuilder> EvaluateAsync(ShardedCommandContext context, string code)
        {
            Discord.Rest.RestUserMessage msg = await context.Channel.SendMessageAsync("Working...");

            code = code.Replace("“", "\"").Replace("‘", "\'").Replace("”", "\"").Replace("’", "\'").Trim('`');
            if (code.Length > 2 && code.Substring(0, 2) == "cs") code = code.Substring(2);

            IEnumerable<Assembly> assemblies = GetAssemblies();

            var sb = new StringBuilder();

            var globals = new Globals
            {
                _client = _client,
                _context = context,
                _guild = context.Guild,
                _channel = context.Channel,
                _user = context.User as SocketGuildUser,
                _services = _services,
                _message = context.Message,
                Console = new FakeConsole(sb),
                _db = _services.GetService<DbService>(),
                _misc = this,
                _audio = _services.GetService<AudioService>(),
                Random = _random,
                _img = _services.GetService<ImageService>(),
                _ms = _services.GetService<MusicService>(),
                _commands = _services.GetService<CommandService>()
            };
            var options = ScriptOptions.Default
                .AddReferences(assemblies)
                .AddImports(globals.Imports)
                .WithAllowUnsafe(true)
                .WithLanguageVersion(LanguageVersion.CSharp8);

            Stopwatch s = Stopwatch.StartNew();
            var script = CSharpScript.Create(code, options, typeof(Globals));
            var compile = script.GetCompilation().GetDiagnostics();
            var cErrors = compile.Where(x => x.Severity == DiagnosticSeverity.Error);
            s.Stop();

            if (cErrors.Any())
            {
                await msg.DeleteAsync();
                return await GenerateErrorAsync(code, cErrors);
            }

            /*
            if (code.WillExit(out string message))
            {
                var ex = new CodeExitException(message, new Exception(message));
                await msg.DeleteAsync();
                return await GenerateErrorAsync(code, ex);
            }
            */

            Stopwatch c = Stopwatch.StartNew();
            ScriptState<object> eval;
            try
            {
                eval = await script.RunAsync(globals);
            }
            catch (Exception e)
            {
                await msg.DeleteAsync();
                return await GenerateErrorAsync(code, e);
            }
            c.Stop();

            var result = eval.ReturnValue;
            if (eval.Exception != null)
            {
                await msg.DeleteAsync();
                return await GenerateErrorAsync(code, eval.Exception);
            }

            string description;
            if (code.Length < 1000)
                 description = $"in: ```cs\n{code}```\nout: \n";
            else
                description = $"in: **[input]({await UploadToBisogaAsync(code)})**\nout: \n";
            string tostringed = result == null ? " " : result.ToString();

            if (result is ICollection r)
                tostringed = r.MakeString();
            else if (result is IReadOnlyCollection<object> x)
                tostringed = x.MakeString();
            else if (result is string str)
                tostringed = str;
            else
                tostringed = result.MakeString();
            
            if (tostringed.Length > 1000)
                description += $"Here is a **[link]({await UploadToBisogaAsync(tostringed)})** to the result.";
            else
                description += $"```{tostringed}```";

            if (sb.ToString().Length > 0)
                description += $"\nConsole: \n```\n{sb}\n```";

            string footer = "";
            if (result is ICollection coll)
                footer += $"Collection has {coll.Count} members • ";
            else if (result is IReadOnlyCollection<object> colle)
                footer += $"Collection has {colle.Count} members • ";

            footer += $"Return type: {(result == null ? "null" : result.GetType().ToString())} • took {s.ElapsedTicks / 10000d} ms to compile and {c.ElapsedTicks / 10000d} ms to execute";


            var em = new EmbedBuilder()
                    .WithFooter(footer)
                    .WithDescription(description)
                    .WithColor(Color.Green);

            await msg.DeleteAsync();
            return em;
        }


        private async Task<EmbedBuilder> GenerateErrorAsync(string code, Exception e)
        {
            bool doCodeBlockForIn = true;
            if (code.Length > 1000)
            {
                code = await UploadToBisogaAsync(code);
                doCodeBlockForIn = false;
            }
            string errorMsg;
            if (e.StackTrace != null)
                errorMsg = $"{e.Message}\n{e.StackTrace.Substring(0, e.StackTrace.IndexOf("---") + 1)}";
            else
                errorMsg = $"{e.Message}";
            bool doCodeBlockForOut = true;
            if (errorMsg.Length > 1000)
            {
                errorMsg = await UploadToBisogaAsync(errorMsg);
                doCodeBlockForOut = false;
            }

            string description;
            if (doCodeBlockForIn)
                description = $"in: ```cs\n{code}```";
            else
                description = $"in:\n**[input]({code})**";

            if (doCodeBlockForOut)
                description += $"\n \nout: \n```{errorMsg}```";
            else
                description += $"\n \nout: \nHere is a **[link]({errorMsg})** to the error message.```";

            var em = new EmbedBuilder()
                    .WithFooter($"{e.GetType()}")
                    .WithDescription(description)
                    .WithColor(Color.Red);
            return em;
        }
        private async Task<EmbedBuilder> GenerateErrorAsync(string code, IEnumerable<Diagnostic> compErrors)
        {
            var msg = new StringBuilder(compErrors.Count());
            foreach (var h in compErrors)
                msg.Append("• " + h.GetMessage() + "\n"); bool doCodeBlockForIn = true;
            if (code.Length > 1000)
            {
                code = await UploadToBisogaAsync(code);
                doCodeBlockForIn = false;
            }
            string errorMsg = msg.ToString();
            bool doCodeBlockForOut = true;
            if (errorMsg.Length > 1000)
            {
                errorMsg = await UploadToBisogaAsync(errorMsg);
                doCodeBlockForOut = false;
            }

            string description;
            if (doCodeBlockForIn)
                description = $"in: ```cs\n{code}```";
            else
                description = $"in:\n**[input]({code})**";

            if (doCodeBlockForOut)
                description += $"\n \nout: \n```{errorMsg}```";
            else
                description += $"\n \nout: \nHere is a **[link]({errorMsg})** to the compilation errors.";

            var em = new EmbedBuilder()
                    .WithFooter(typeof(CompilationErrorException).ToString())
                    .WithDescription(description)
                    .WithColor(Color.Red);
            return em;
        }

        private static IEnumerable<Assembly> GetAssemblies()
        {
            var assemblies = Assembly.GetEntryAssembly().GetReferencedAssemblies();
            foreach (var a in assemblies)
            {
                var s = Assembly.Load(a);
                yield return s;
            }
            yield return Assembly.GetEntryAssembly();
            yield return typeof(ILookup<string, string>).GetTypeInfo().Assembly;
        }

        public async Task<string> UploadToBisogaAsync(string stuffToUpload)
        {
            try
            {
                using (WebClient cl = new WebClient())
                {
                    cl.Headers.Add(HttpRequestHeader.ContentType, "application/json");
                    var encoded = JsonConvert.SerializeObject(new Dictionary<string, string> { { "text", stuffToUpload } });

                    return await cl.UploadStringTaskAsync("https://bisoga.xyz/api/paste", encoded);
                }
            }
            catch (Exception e) when (e.Message == "The remote server returned an error: (520) Origin Error.")
            {
                return "https://github.com/jakedacatman/OscarBot/blob/master/sorry.md";
            }
        }

        public int GenerateId()
        {
            int generated;
            do
            {
                generated = _random.Next();
            }
            while (ids.Contains(generated));
            ids.Add(generated);
            return generated;
        }
    }
}
