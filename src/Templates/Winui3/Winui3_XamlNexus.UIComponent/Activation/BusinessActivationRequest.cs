using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Winui3_XamlNexus.UIComponent.Utils;

namespace Winui3_XamlNexus.UIComponent.Activation;

/// <summary>A business destination from launch arguments. Data is kept separate from framework payload keys.</summary>
public sealed class BusinessActivationRequest {
    public const string PayloadKey = "XamlNexus.Activation.Request";
    public string Route { get; }
    public JsonElement Data { get; }
    public IReadOnlyList<string> Arguments { get; }

    private BusinessActivationRequest(string route, JsonElement data, IReadOnlyList<string> arguments) {
        Route = route;
        Data = data;
        Arguments = Array.AsReadOnly(arguments.ToArray());
    }

    /// <summary>Returns null for ordinary launches; rejects malformed or duplicate reserved options.</summary>
    public static BusinessActivationRequest? FromArguments(IReadOnlyList<string> arguments) {
        ArgumentNullException.ThrowIfNull(arguments);
        string? route = null, json = null;
        for (int i = 0; i < arguments.Count; i++) {
            string option = arguments[i];
            if (option != "--route" && option != "--data") continue;
            if (++i >= arguments.Count || arguments[i] == "--route" || arguments[i] == "--data")
                throw new FormatException($"Missing value for {option}.");
            if (option == "--route") {
                if (route is not null) throw new FormatException("Duplicate --route.");
                route = arguments[i];
            }
            else {
                if (json is not null) throw new FormatException("Duplicate --data.");
                json = arguments[i];
            }
        }
        if (route is null && json is null) return null;
        if (string.IsNullOrWhiteSpace(route)) throw new FormatException("--route is required and cannot be empty.");
        try {
            using var document = JsonDocument.Parse(json ?? "{}");
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new FormatException("--data must be a JSON object.");
            return new BusinessActivationRequest(route, document.RootElement.Clone(), arguments);
        }
        catch (JsonException exception) { throw new FormatException("Invalid JSON in --data.", exception); }
    }
}

/// <summary>Reads activation data or a legacy in-app payload using the same business field name.</summary>
public static class BusinessActivationPayloadExtensions {
    public static string? GetBusinessString(this FrameworkPayload? payload, string key) {
        if (payload is not null && payload.TryGet<BusinessActivationRequest>(BusinessActivationRequest.PayloadKey, out var request)) {
            return request.Data.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() : null;
        }
        return payload is not null && payload.TryGet<string>(key, out var text) ? text : null;
    }
}
