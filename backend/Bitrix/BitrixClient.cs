using System.Text.Json;
using System.Net;
using Microsoft.Extensions.Options;

namespace InformesAvanzar.Api.Bitrix;

public interface IBitrixClient
{
    Task<JsonDocument> CallAsync(
        string method,
        IEnumerable<KeyValuePair<string, string>> parameters,
        CancellationToken cancellationToken);
}

public sealed class BitrixClient(HttpClient httpClient, IOptions<BitrixOptions> options) : IBitrixClient
{
    private readonly BitrixOptions _options = options.Value;

    public async Task<JsonDocument> CallAsync(
        string method,
        IEnumerable<KeyValuePair<string, string>> parameters,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookUrl))
        {
            throw new InvalidOperationException("Bitrix:WebhookUrl or BITRIX_WEBHOOK_URL is required.");
        }

        var baseUrl = _options.WebhookUrl.TrimEnd('/');
        var requestUri = $"{baseUrl}/{method}.json";
        var requestParameters = parameters.ToArray();
        for (var attempt = 0; ; attempt++)
        {
            using var content = new FormUrlEncodedContent(requestParameters);
            using var response = await httpClient.PostAsync(requestUri, content, cancellationToken);
            if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < 6)
            {
                var retryAfter = response.Headers.RetryAfter?.Delta
                    ?? TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, attempt)));
                await Task.Delay(retryAfter, cancellationToken);
                continue;
            }

            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        }
    }
}
