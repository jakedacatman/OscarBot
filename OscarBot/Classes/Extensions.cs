using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Discord;
namespace OscarBot.Classes
{

    public static class IEnumerableExtensions
    {
        public static string ToString<T>(this IEnumerable<T> t, string separator)
        {
            return string.Join(separator, t);
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

    public static class ConcurrentDictionaryExtensions
    {
        public static bool Update<T, H>(this ConcurrentDictionary<T, H> dict, T key, H thing)
        {
            var didRemove = dict.TryRemove(key, out H _);
            var didAdd = dict.TryAdd(key, thing);
            return didRemove && didAdd;
        }
    }
}
