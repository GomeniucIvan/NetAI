using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using NetAI.Api.Serialization;

namespace NetAI.Api.Models.Configuration;

[JsonConverter(typeof(JsonStringEnumMemberConverter))]
public enum AppMode
{
    [EnumMember(Value = "oss")]
    Oss,

    [EnumMember(Value = "saas")]
    Saas
}
