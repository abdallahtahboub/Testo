using System.Text.Json.Serialization;
using Testo.Contracts.Execution;

namespace Testo.Worker;

/// <summary>
/// How a job and a result are written on the wire between the application and the worker.
/// </summary>
/// <remarks>
/// Generated rather than reflected, so the protocol is a compile-time thing: a field that stops
/// existing stops compiling, rather than quietly arriving as null in a process that has no
/// database to check it against.
/// </remarks>
[JsonSourceGenerationOptions(WriteIndented = false, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(WorkerJob))]
[JsonSerializable(typeof(WorkerResult))]
internal sealed partial class WorkerJsonContext : JsonSerializerContext;
