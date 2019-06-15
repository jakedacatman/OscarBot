using System;
using System.Collections;
using System.Numerics;
using System.Collections.Generic;
using System.Text;
using Discord;
using System.Linq;
using System.Reflection;

namespace OscarBot.Classes
{

    public static class IEnumerableExtensions
    {
        public static string MakeString(this IEnumerable t, int level = 0)
        {
            if (t == null || !t.Cast<object>().Any())
                return $"[\n{"  ".RepeatString(level)}]";

            StringBuilder sb = new StringBuilder();
            foreach (var thing in t)
            {
                if (thing == null)
                    continue;

                var toAppend = "  ".RepeatString(level + 1);

                if (thing is ICollection h)
                    toAppend += h.MakeString(level + 1);
                else if (thing is IReadOnlyCollection<object> x)
                    toAppend += x.MakeString(level + 1);
                else
                    toAppend += thing.ToString();

                toAppend += ",\n";

                sb.Append(toAppend);
            }
            var str = sb.ToString();
            return $"[\n{str.Substring(0, str.Length - 2)}\n{"  ".RepeatString(level)}]";
        }
    }

    public static class ObjectExtensions
    {
        public static string MakeString(this object t, int level = 0)
        {
            if (t == null)
                return $"[\n{"  ".RepeatString(level)}]";

            if (t.GetType().IsValueType) return t.ToString();

            StringBuilder sb = new StringBuilder();
            var properties = t.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic).Where(x => x.GetIndexParameters().Length == 0);
            foreach (var thing in properties)
            {
                if (thing == null)
                    continue;

                var toAppend = "  ".RepeatString(level + 1);

                var value = thing.GetValue(t);

                if (value is ICollection h)
                    toAppend += $"{thing.Name}:\n  {h.MakeString(level + 1)}";
                else if (value is IReadOnlyCollection<object> x)
                    toAppend += $"{thing.Name}:\n  {x.MakeString(level + 1)}";
                else
                    toAppend += $"{thing.Name}: {value ?? "null"}";

                toAppend += ",\n";

                sb.Append(toAppend);
            }
            var str = sb.ToString();
            if (str.Length == 0) return $"{t.ToString()} (no properties)";
            return $"[\n{str.Substring(0, str.Length - 2)}\n{"  ".RepeatString(level)}]";
        }

        public static IEnumerable<object> Repeat(this object o, int amount) => Enumerable.Repeat(o, amount);
        public static string RepeatString(this string o, int amount) => string.Join(string.Empty, o.Repeat(amount));
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

    public static class BigIntegerExtensions
    {
        public static BigInteger Factorial(this BigInteger i)
        {
            BigInteger h = 1;
            for (BigInteger q = 1; q <= i; q++)
                h *= q;

            return h;
        }
    }
}
