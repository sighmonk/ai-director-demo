using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Minimal Director action used by the LLM policy tests.</summary>
public partial class LlmTestAction : DirectorAction
{
    protected override bool OnExecute() => true;
}

/// <summary>Returns scripted responses (or throws) and counts calls; never touches the network.</summary>
public sealed class FakeLlmClient : ILlmClient
{
    private readonly Queue<Func<LlmResponse>> _responses = new();

    public FakeLlmClient(params Func<LlmResponse>[] responses)
    {
        foreach (Func<LlmResponse> response in responses) _responses.Enqueue(response);
    }

    public int Calls { get; private set; }

    public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        Calls++;
        if (_responses.Count == 0) throw new InvalidOperationException("No scripted response left.");
        return Task.FromResult(_responses.Dequeue()());
    }
}

/// <summary>Controllable wall clock for latency and deadline tests.</summary>
public sealed class FakeClock
{
    public DateTimeOffset Now { get; set; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public DateTimeOffset Read() => Now;

    public void Advance(double milliseconds) => Now = Now.AddMilliseconds(milliseconds);
}
