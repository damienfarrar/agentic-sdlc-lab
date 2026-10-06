namespace Lab.Tests.ClientService;

/// <summary>Returns a canned response and records the request, so client tests need no network.</summary>
internal sealed class StubHttpMessageHandler(Func<HttpResponseMessage> respond) : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        return Task.FromResult(respond());
    }
}
