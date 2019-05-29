using System;
using System.Collections.Generic;   
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Discord.Commands;
using OscarBot.Services;
using OscarBot.Classes;
using System.Reflection;
using System.Net;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Scripting;
using Discord.Addons.Interactive;
using System.Diagnostics;
using Newtonsoft.Json;

namespace OscarBot.Modules
{
    [Name("Miscellaneous")]
    public class MiscModule : InteractiveBase<ShardedCommandContext>
    {
        private readonly DiscordShardedClient _client;

        private readonly MiscService _misc;
        private readonly DbService _db;

        public MiscModule(DiscordShardedClient client, MiscService misc, DbService db)
        {
            _client = client;
            _misc = misc;
            _db = db;
        }

        [Command("eval")]
        [Alias("evaluate")]
        [Summary("Evaluates C# code.")]
        [RequireOwner]
        public async Task EvalCmd([Summary("The code to evaluate."), Remainder] string code)
        {
            try
            {
                Discord.Rest.RestUserMessage msg = await Context.Channel.SendMessageAsync("Working...");
                var mobile = false;

                code = code.Replace("“", "\"").Replace("‘", "\'").Replace("”", "\"").Replace("’", "\'").Trim('`');
                if (code.Length > 2 && code.Substring(0, 2) == "cs") code = code.Substring(2);

                if (code.Length > 8 && code.Substring(code.Length - 8) == "--mobile")
                {
                    mobile = true;
                    code = code.Substring(0, code.Length - 8);
                }

                IEnumerable<Assembly> assemblies = GetAssemblies();

                var sb = new StringBuilder();

                var globals = new Globals
                {
                    Client = Context.Client,
                    Context = Context,
                    Guild = Context.Guild,
                    Channel = Context.Channel as SocketGuildChannel,
                    User = Context.User as SocketGuildUser,
                    Message = Context.Message,
                    Console = new FakeConsole(sb),
                    _db = _db
                };
                var options = ScriptOptions.Default
                    .AddReferences(assemblies)
                    .AddImports(globals.Imports);

                Stopwatch s = Stopwatch.StartNew();
                var script = CSharpScript.Create(code, options, typeof(Globals));
                var compile = script.GetCompilation().GetDiagnostics();
                var cErrors = compile.Where(x => x.Severity == DiagnosticSeverity.Error);
                s.Stop();

                if (cErrors.Count() > 0)
                {
                    await msg.ModifyAsync(async x =>
                    {
                        x.Content = string.Empty;
                        x.Embed = (await GenerateErrorAsync(code, cErrors)).Build();
                    });
                    return;
                }

                if (code.WillExit(out string message))
                {
                    var ex = new CodeExitException(message, new Exception(message));
                    await msg.ModifyAsync(async x =>
                    {
                        x.Content = string.Empty;
                        x.Embed = (await GenerateErrorAsync(code, ex)).Build();
                    });
                }

                Stopwatch c = Stopwatch.StartNew();
                ScriptState<object> eval;
                try
                {
                    eval = await script.RunAsync(globals);
                }
                catch (Exception e)
                {
                    await msg.ModifyAsync(async x =>
                    {
                        x.Content = string.Empty;
                        x.Embed = (await GenerateErrorAsync(code, e)).Build();
                    });
                    return;
                }
                c.Stop();

                var result = eval.ReturnValue;
                if (eval.Exception != null)
                {
                    await ReplyAsync(embed: (await GenerateErrorAsync(code, eval.Exception)).Build());
                    return;
                }

                if (mobile)
                {
                    await msg.ModifyAsync(x => x.Content = $"`{(result == null ? sb : sb.Append(result.ToString()))}`\n`Return type: {(result == null ? typeof(void) : result.GetType())} • took {s.ElapsedTicks / 10000d} ms to compile and {c.ElapsedTicks / 10000d} ms to execute`");
                }
                else
                {
                    string description;
                    if (result == null || (result.ToString().Length == 0 && sb.Length > 0))
                        description = $"in: ```cs\n{code}```\nConsole: \n```\n{sb}\n```";
                    else if (sb.Length > 0)
                        description = $"in: ```cs\n{code}```\nout: \n```{result}```\n Console: \n ```\n{sb}\n```";
                    else if (string.IsNullOrEmpty(result.ToString()))
                        description = $"in: ```cs\n{code}```\nout: \n```\n```";
                    else
                        description = $"in: ```cs\n{code}```\nout: \n```{result}```";

                    var em = new EmbedBuilder()
                            .WithFooter($"Return type: {(result == null ? typeof(void) : result.GetType())} • took {s.ElapsedTicks / 10000d} ms to compile and {c.ElapsedTicks / 10000d} ms to execute")
                            .WithDescription(description.Length < 2048 ? description : $"in: ```cs\n{code}```\n \nout: **Output was too long for the embed, so here's a [link]({await UploadToBisogaAsync(result.ToString())}) to the result:**")
                            .WithColor(Color.Green);

                    await msg.ModifyAsync(x =>
                    {
                        x.Content = string.Empty;
                        x.Embed = em.Build();
                    });
                    await msg.AddReactionAsync(new Emoji("✔"));
                }
            }
            catch (Exception e)
            {
                await ReplyAsync(embed: _misc.GenerateErrorMessage(e).Build());
            }
        }

        private async Task<EmbedBuilder> GenerateErrorAsync(string code, Exception e)
        {
            string description;
            if (e.StackTrace == null)
                description = $"in: ```cs\n{code}```\n \nout: \n```{e.Message}```";
            else description = $"in: ```cs\n{code}```\n \nout: \n```{e.Message}\n{e.StackTrace.Substring(0, e.StackTrace.IndexOf("---") + 1)}```";

            var em = new EmbedBuilder()
                    .WithFooter($"{e.GetType()}")
                    .WithDescription(description.Length < 2048 ? description : $"in: ```cs\n{code}```\n \nout: **Error was too long for the embed, so here's a [link]({await UploadToBisogaAsync($"{e.Message}\n{e.StackTrace.Substring(0, e.StackTrace.IndexOf("---") + 1)}")}) to the result:**")
                    .WithColor(Color.Red);
            return em;
        }
        private async Task<EmbedBuilder> GenerateErrorAsync(string code, IEnumerable<Diagnostic> compErrors)
        {
            var msg = new StringBuilder(compErrors.Count());
            foreach (var h in compErrors)
                msg.Append("• " + h.GetMessage() + "\n");

            var description = $"in: ```cs\n{code}```\n \nout: \n```{msg}```";

            var em = new EmbedBuilder()
                    .WithFooter(typeof(CompilationErrorException).ToString())
                    .WithDescription(description.Length < 1000 ? description : $"in: ```cs\n{code}```\n \nout: **Error was too long for the embed, so here's a [link]({await UploadToBisogaAsync(msg.ToString())} to the result:**")
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

        private async Task<string> UploadToBisogaAsync(string stuffToUpload)
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
