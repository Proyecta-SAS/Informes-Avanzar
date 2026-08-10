namespace InformesAvanzar.Api.Bitrix;

public sealed class BitrixOptions
{
    public string? WebhookUrl { get; set; }
    public string? OutgoingWebhookToken { get; set; }
    public string? WebhookAllowedPipelineDomains { get; set; }
    public string? Scopes { get; set; }
    public string? SecretName { get; set; }
}
