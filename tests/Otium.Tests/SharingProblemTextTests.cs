using Otium.Core.Bandwidth;

namespace Otium.Tests;

public class SharingProblemTextTests
{
    [Fact]
    public void A_timeout_is_explained_in_plain_words()
    {
        var timeout = new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout of 30 seconds elapsing.", new TimeoutException());
        Assert.Equal("The sharing service didn't answer in time. Otium tries again in a moment.", BandwidthSharer.ProblemText(timeout));
    }

    [Fact]
    public void A_failed_connection_and_other_errors_keep_their_own_wording()
    {
        Assert.StartsWith("Couldn't reach the sharing service", BandwidthSharer.ProblemText(new HttpRequestException("no route")));
        Assert.Equal("Something went wrong: boom", BandwidthSharer.ProblemText(new InvalidOperationException("boom")));
    }
}
