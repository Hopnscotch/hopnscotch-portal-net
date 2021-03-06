using Hopnscotch.Integration.AmoCRM;
using Hopnscotch.Integration.AmoCRM.Annotations;
using Newtonsoft.Json;

namespace Hopnscotch.Portal.Integration.AmoCRM.Entities
{
    [UsedImplicitly]
    [AmoCrmResponseType("Contacts")]
    public sealed class ApiContactListResponse : ApiListResponseBase<ApiIndividualContactResponse>
    {
        [JsonProperty("contacts")]
        public override ApiIndividualContactResponse[] Entities { get; set; }
    }
}