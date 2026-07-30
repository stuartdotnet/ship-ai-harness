using System.ClientModel;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

namespace ShipAI.ConsoleHost;

/// <summary>
/// Builds the <see cref="IChatClient"/> that AURORA runs on.
/// </summary>
/// <remarks>
/// <para>
/// This is the whole model-provider seam. <c>AsHarnessAgent</c> is an extension on
/// <see cref="IChatClient"/>, so swapping Azure OpenAI for Anthropic, OpenAI, or a local
/// model means returning a different client from this one method. Nothing downstream —
/// not the tools, not the factory, not the console — knows or cares which model is behind it.
/// </para>
/// <para>
/// A note on Foundry: <c>Azure.AI.Projects</c> exposes no chat-client entry point, so a
/// Foundry deployment is reached through <see cref="AzureOpenAIClient"/> pointed at the
/// project's OpenAI endpoint rather than through <c>AIProjectClient</c>.
/// </para>
/// </remarks>
internal static class ChatClientFactory
{
    public static IChatClient Create(IConfiguration configuration)
    {
        var endpoint = Require(configuration, "AZURE_OPENAI_ENDPOINT");
        var deployment = Require(configuration, "AZURE_OPENAI_DEPLOYMENT");
        var apiKey = configuration["AZURE_OPENAI_API_KEY"];

        // Key auth when a key is supplied, Entra ID otherwise. Entra is the better default
        // for a repo people will clone: nothing to leak if they commit their .env by mistake.
        var azureClient = string.IsNullOrWhiteSpace(apiKey)
            ? new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
            : new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey));

        return azureClient.GetChatClient(deployment).AsIChatClient();
    }

    private static string Require(IConfiguration configuration, string key)
        => configuration[key] is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException(
                $"""
                 Configuration value '{key}' is missing.

                 Copy .env.example to .env and fill it in, or set the value as an environment
                 variable or user secret. See README.md for the 60-second quickstart.
                 """);
}
