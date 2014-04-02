using Hopnscotch.Integration.AmoCRM.Annotations;
using Newtonsoft.Json;

namespace Hopnscotch.Portal.Integration.AmoCRM.Entities
{
    [UsedImplicitly]
    public sealed class ApiAccountRootResponse
    {
        [JsonProperty("account")]
        public ApiAccountResponse Account { get; set; }
    }
}