/// JSON shape shared by the REST and MCP adapters (F# unions as plain strings, camelCase).
module Logistics.Api.Json

open System.Text.Encodings.Web
open System.Text.Json
open System.Text.Json.Serialization

let configure (options: JsonSerializerOptions) =
    options.Converters.Add(JsonFSharpConverter(JsonFSharpOptions.Default().WithUnionUnwrapFieldlessTags()))

/// For MCP tool text: read by a model, never embedded in HTML, so `->` stays `->` instead of `>`.
let options =
    let o =
        JsonSerializerOptions(JsonSerializerDefaults.Web, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping)

    configure o
    o

let serialize value = JsonSerializer.Serialize(value, options)
