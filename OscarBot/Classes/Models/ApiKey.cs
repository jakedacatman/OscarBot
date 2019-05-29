using System.ComponentModel.DataAnnotations;

namespace OscarBot.Classes
{
    public class ApiKey
    {
        [Key]
        public string Service { get; set; }
        public string Key { get; set; }
    }
}
