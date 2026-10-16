using Otium.Core.Accounts;

namespace Otium.Tests;

public class InviteMessageTests
{
    private const string Code = "K7MQ4ZP";
    private const string Link = "https://otium.example/r/K7MQ4ZP";

    private static string Message(int style = 0) =>
        InviteMessages.Fill(InviteMessages.Styles[style].Template, Code, Link, TimeSpan.FromDays(30));

    [Fact]
    public void Every_ready_made_message_carries_the_code_link_and_offer()
    {
        Assert.True(InviteMessages.Styles.Count >= 3);
        for (var i = 0; i < InviteMessages.Styles.Count; i++)
        {
            var message = Message(i);
            Assert.Contains(Code, message);
            Assert.Contains(Link, message);
            Assert.Contains("30 days", message);
            Assert.DoesNotContain("{", message);
        }
    }

    [Fact]
    public void Discord_gets_the_code_in_bold_and_a_bare_link_so_it_unfurls()
    {
        var discord = InviteMessages.ForDiscord(Message(), Code, Link);

        Assert.Equal($"Join Otium with my code **{Code}** and get 30 days fee-free: {Link}", discord);
        Assert.Equal("No code here", InviteMessages.ForDiscord("No code here", Code, Link));
    }

    [Fact]
    public void Email_keeps_the_code_and_link_even_if_they_were_edited_out()
    {
        Assert.Equal($"Hi,\n\n{Message(1)}\n", InviteMessages.ForEmail(Message(1), Code, Link));

        var edited = InviteMessages.ForEmail("Try this app!", Code, Link);
        Assert.Contains($"My invite code: {Code}", edited);
        Assert.Contains($"Download: {Link}", edited);
    }

    [Fact]
    public void Share_links_are_fully_encoded()
    {
        var whatsapp = InviteMessages.WhatsAppLink("Hi & welcome: 100% fee-free?");
        Assert.Equal("https://wa.me/?text=Hi%20%26%20welcome%3A%20100%25%20fee-free%3F", whatsapp);

        var email = InviteMessages.EmailLink("Subject & more", "Line 1\nLine 2");
        Assert.Equal("mailto:?subject=Subject%20%26%20more&body=Line%201%0ALine%202", email);
    }

    [Theory]
    [InlineData("K7MQ4ZP", "K7M••••")]
    [InlineData("AB", "AB")]
    public void Leaderboard_names_mask_most_of_the_code(string code, string masked)
    {
        Assert.Equal(masked, ReferralCodes.Mask(code));
    }
}
