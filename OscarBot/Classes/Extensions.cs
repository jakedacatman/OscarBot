using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Discord;
using System.Linq;

namespace OscarBot.Classes
{

    public static class IEnumerableExtensions
    {
        public static string MakeString(this IEnumerable t, int level = 0)
        {
            if (t == null || !t.Cast<object>().Any())
                return "[\n]";

            StringBuilder sb = new StringBuilder();
            foreach (var thing in t)
            {
                if (thing == null)
                    continue;

                var toAppend = string.Join(string.Empty, Enumerable.Repeat("  ", level + 1));

                if (thing is ICollection h)
                    toAppend += h.MakeString(level + 1);
                else
                    toAppend += thing.ToString();

                toAppend += ",\n";

                sb.Append(toAppend);
            }
            var str = sb.ToString();
            return $"[\n{str.Substring(0, str.Length - 2)}\n{string.Join(string.Empty, Enumerable.Repeat("  ", level))}]";
        }
    }

    public static class IUserExtensions
    {
        public static bool IsQueuer(this IUser user, Song s)
        {
            return user.Id == s.QueuerId;
        }
    }

    public static class StringExtensions
    {
        public static bool WillExit(this string s, out string message)
        {
            if (s.Contains("Environment.Exit"))
            {
                message = "This code calls Environment.Exit.";
                return true;
            }
            message = "This code will not exit.";
            return false;
        }
    }
}
