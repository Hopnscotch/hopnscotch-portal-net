using Newtonsoft.Json;

namespace Hopnscotch.Portal.Integration.AmoCRM.Entities
{
    internal class ApiAuthParameters
    {
        [JsonProperty("USER_LOGIN")]
        public string Login { get; private set; }

        [JsonProperty("USER_HASH")]
        public string Hash { get; private set; }

        public ApiAuthParameters(string login, string hash)
        {
            Login = login;
            Hash = hash;
        }
    }
}