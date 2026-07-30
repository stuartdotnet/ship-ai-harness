using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace ShipAI.Agent.Tests;

/// <summary>
/// An <see cref="IChatClient"/> that answers with a canned reply and records what it was asked.
/// </summary>
/// <remarks>
/// Keeps the agent-layer tests free of model calls, which keeps <c>dotnet test</c> fast,
/// offline, and deterministic. The harness composes a substantial pipeline around whatever
/// client it is handed, so this is enough to assert on how that pipeline was configured.
/// </remarks>
internal sealed class FakeChatClient(string reply = "Acknowledged.") : IChatClient
{
    public List<ChatOptions?> ObservedOptions { get; } = [];

    public List<ChatMessage> ObservedMessages { get; } = [];

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ObservedOptions.Add(options);
        ObservedMessages.AddRange(messages);

        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, reply)));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);

        foreach (var update in response.ToChatResponseUpdates())
        {
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
        => serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
        // Nothing to release.
    }
}
