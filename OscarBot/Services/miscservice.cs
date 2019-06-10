using System;
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
using Victoria;
using System.Net;
using Discord.Commands;

namespace OscarBot.Services
{
    public class MiscService
    {
        private readonly DiscordShardedClient _client;
        private readonly DbService _db;

        private readonly LavaShardClient _manager;
        private readonly LavaRestClient _lavaRestClient;

        public MiscService(DiscordShardedClient client, DbService db, LavaShardClient manager, LavaRestClient lavaRestClient)
        {
            _client = client;
            _db = db;
            _manager = manager;
            _lavaRestClient = lavaRestClient;
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

        public async Task<EmbedBuilder> EvaluateAsync(ShardedCommandContext context, string code)
        {
            Discord.Rest.RestUserMessage msg = await context.Channel.SendMessageAsync("Working...");

            code = code.Replace("“", "\"").Replace("‘", "\'").Replace("”", "\"").Replace("’", "\'").Trim('`');
            if (code.Length > 2 && code.Substring(0, 2) == "cs") code = code.Substring(2);

            IEnumerable<Assembly> assemblies = GetAssemblies();

            var sb = new StringBuilder();

            var globals = new Globals
            {
                Client = context.Client,
                Context = context,
                Guild = context.Guild,
                Channel = context.Channel as SocketGuildChannel,
                User = context.User as SocketGuildUser,
                Message = context.Message,
                Console = new FakeConsole(sb),
                _db = _db,
                _misc = this,
                _lavaRestClient = _lavaRestClient,
                _manager = _manager,
               Random = new Random()
            };
            var options = ScriptOptions.Default
                .AddReferences(assemblies)
                .AddImports(globals.Imports);

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

            if (code.WillExit(out string message))
            {
                var ex = new CodeExitException(message, new Exception(message));
                await msg.DeleteAsync();
                return await GenerateErrorAsync(code, ex);
            }

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
            string tostringed = result == null ? "" : result.ToString();

            if (result is ICollection r)
            {
                description += $"```{r.MakeString()}```";
                tostringed = r.MakeString();
            }
            else if (result == null || string.IsNullOrEmpty(result.ToString()))
                description += $"``` ```";
            else if (tostringed.Length > 1000)
                description += $"Here is a **[link]({await UploadToBisogaAsync(tostringed)})** to the result.";
            else
                description += $"```{tostringed}```";

            if (sb.ToString().Length > 0)
                description += $"\nConsole: \n```\n{sb}\n```";

            var em = new EmbedBuilder()
                    .WithFooter($"Return type: {(result == null ? "null" : result.GetType().ToString())} • took {s.ElapsedTicks / 10000d} ms to compile and {c.ElapsedTicks / 10000d} ms to execute")
                    .WithDescription(description.Length < 2048 ? description : $"in: ```cs\n{code}```\n \nout: **Output was too long for the embed, so here's a [link]({await UploadToBisogaAsync(tostringed)}) to the result:**")
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
            using (WebClient cl = new WebClient())
            {
                cl.Headers.Add(HttpRequestHeader.ContentType, "application/json");
                var encoded = JsonConvert.SerializeObject(new Dictionary<string, string> { { "text", stuffToUpload } });

                return await cl.UploadStringTaskAsync("https://bisoga.xyz/api/paste", encoded);
            }
        }
    }
}
