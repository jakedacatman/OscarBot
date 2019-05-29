using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OscarBot.Classes
{
    public class CodeExitException : Exception
    {
        public string Method { get; }
        public CodeExitException(string method, Exception e) : base("This code will cause the bot to exit.")
        {
            Method = method;
        }
    }
}
