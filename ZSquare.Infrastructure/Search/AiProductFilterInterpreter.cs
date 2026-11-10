using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using ZSquare.Application.Search;

namespace ZSquare.Infrastructure.Search;

public sealed class AiProductFilterInterpreter(HttpClient httpClient, IConfiguration configuration)
    : IProductFilterInterpreter
{
    private const string ChatCompletionsEndpoint = "https://api.groq.com/openai/v1/chat/completions";

    public async Task<string> InterpretAsync(
        string systemPrompt,
        string userQuery,
        CancellationToken cancellationToken = default)
    {
        var apiKey = configuration["Ai:ApiKey"];
        var model = configuration["Ai:Model"];
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(model))
            throw new InvalidDataException("AI model configuration or API key is missing.");

        using var request = new HttpRequestMessage(HttpMethod.Post, ChatCompletionsEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = JsonContent.Create(new
        {
            model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userQuery }
            },
            max_completion_tokens = 300,
            response_format = new { type = "json_object" }
        });

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (json.RootElement.TryGetProperty("choices", out var choices) &&
            choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0 &&
            choices[0].TryGetProperty("message", out var message) &&
            message.TryGetProperty("content", out var content) && content.GetString() is { } value)
        {
            return value;
        }

        throw new InvalidDataException("AI response contained no text block.");
    }
}
