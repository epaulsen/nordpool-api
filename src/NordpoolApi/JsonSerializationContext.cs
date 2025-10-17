using System.Text.Json.Serialization;
using NordpoolApi.Models;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("NordpoolApi.Tests")]

namespace NordpoolApi;

[JsonSerializable(typeof(NordpoolData))]
[JsonSerializable(typeof(MultiAreaEntry))]
[JsonSerializable(typeof(ElectricityPrice))]
[JsonSerializable(typeof(QuarterlyPrice))]
[JsonSerializable(typeof(HealthCheckResponse))]
[JsonSerializable(typeof(IEnumerable<ElectricityPrice>))]
[JsonSerializable(typeof(Dictionary<string, decimal>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(List<MultiAreaEntry>))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}
