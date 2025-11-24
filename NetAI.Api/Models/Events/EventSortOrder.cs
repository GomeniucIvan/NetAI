using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using NetAI.Api.Serialization;

namespace NetAI.Api.Models.Events;

[JsonConverter(typeof(JsonStringEnumMemberConverter))]
public enum EventSortOrder
{
    [EnumMember(Value = "timestamp")]
    Timestamp,

    [EnumMember(Value = "timestamp_desc")]
    TimestampDesc
}
