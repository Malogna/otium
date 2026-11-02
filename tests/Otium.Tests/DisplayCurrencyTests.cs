using System.Globalization;
using Otium.Core.Currency;

namespace Otium.Tests;

/// <summary>Showing amounts in the currency of the computer's region, converted from the dollars everything is worked out in.</summary>
public class DisplayCurrencyTests
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");
    private static readonly CultureInfo British = CultureInfo.GetCultureInfo("en-GB");
    private static readonly CultureInfo American = CultureInfo.GetCultureInfo("en-US");

    private static CurrencyRatesCache Rates() => new()
    {
        AsOf = new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero),
        FetchedAt = new DateTimeOffset(2026, 11, 2, 6, 0, 0, TimeSpan.Zero),
        Rates = new() { ["EUR"] = 0.92, ["GBP"] = 0.77, ["JPY"] = 150, ["SEK"] = 10.5 },
    };

    [Theory]
    [InlineData(1234.5, "$1,234.50")]
    [InlineData(12, "$12.00")]
    [InlineData(0.4, "$0.400")]   // below a dollar: one more digit, because most earnings are fractions of a dollar
    [InlineData(0, "$0.000")]
    [InlineData(-0.4, "-$0.400")]
    [InlineData(-2.5, "-$2.50")]
    public void Dollars_are_written_the_way_they_always_were(double usd, string expected) =>
        Assert.Equal(expected, DisplayCurrency.Usd.Format(usd));

    [Fact]
    public void A_dash_is_never_shown_for_a_negative_amount_that_rounds_to_zero() =>
        Assert.Equal("$0.000", DisplayCurrency.Usd.Format(-0.0000001));

    [Fact]
    public void Euros_follow_the_number_style_of_the_german_locale()
    {
        var euro = new DisplayCurrency("EUR", 0.92, German);

        Assert.Equal("1.150,00 €", euro.Format(1250));
        Assert.Equal("0,368 €", euro.Format(0.4));
    }

    [Fact]
    public void Pounds_for_a_british_user_and_euros_shown_to_an_american_use_their_own_symbols()
    {
        Assert.Equal("£7.70", new DisplayCurrency("GBP", 0.77, British).Format(10));
        Assert.Equal("€9.20", new DisplayCurrency("EUR", 0.92, American).Format(10));
    }

    [Fact]
    public void The_extra_digit_follows_the_amount_in_the_currency_shown_not_in_dollars()
    {
        var krone = new DisplayCurrency("NOK", 9.2, CultureInfo.GetCultureInfo("nb-NO"));

        Assert.EndsWith("4,60", krone.Format(0.5));     // 4.60 kroner: not "4,600", which reads as four thousand
        Assert.EndsWith("0,460", krone.Format(0.05));   // under one krone: the extra digit helps
        Assert.EndsWith("2,30", krone.Format(0.25, cents: true));
    }

    [Fact]
    public void Currencies_without_a_minor_unit_show_no_decimals_once_they_are_worth_a_few_units()
    {
        var yen = new DisplayCurrency("JPY", 150, CultureInfo.InvariantCulture);

        Assert.Contains("1,500", yen.Format(10));
        Assert.DoesNotContain(".", yen.Format(10));
        Assert.Contains("7.50", yen.Format(0.05)); // 7.5 yen: two decimals under ten
    }

    [Fact]
    public void An_unknown_symbol_falls_back_to_the_code()
    {
        var krona = new DisplayCurrency("SEK", 10.5, American);

        Assert.Equal("SEK 105.00", krona.Format(10));
    }

    [Fact]
    public void Amounts_owed_in_dollars_say_so_next_to_the_converted_amount()
    {
        Assert.Equal("$5.00", DisplayCurrency.Usd.FormatWithUsd(5m));
        Assert.Equal("$5.00 (≈ €4.60)", new DisplayCurrency("EUR", 0.92, American).FormatWithUsd(5m));
    }

    [Fact]
    public void Converting_back_and_forth_gives_the_same_amount()
    {
        var euro = new DisplayCurrency("EUR", 0.92, German);

        Assert.Equal(10, euro.ToUsd(euro.FromUsd(10)), 9);
    }

    [Theory]
    [InlineData("de-DE", "EUR")]
    [InlineData("en-GB", "GBP")]
    [InlineData("en-US", "USD")]
    [InlineData("ja-JP", "JPY")]
    [InlineData("en", "USD")]      // a language with no region says nothing
    [InlineData("", "USD")]        // the invariant culture
    public void The_region_decides_the_currency_when_nothing_is_chosen(string culture, string code) =>
        Assert.Equal(code, DisplayCurrency.RegionCode(CultureInfo.GetCultureInfo(culture)));

    [Fact]
    public void Automatic_uses_the_regions_currency_when_there_is_a_rate_for_it()
    {
        var shown = CurrencyChoice.Resolve(CurrencyChoice.Automatic, German, Rates());

        Assert.Equal("EUR", shown.Code);
        Assert.Equal(0.92, shown.PerUsd);
    }

    [Fact]
    public void An_explicit_choice_wins_over_the_region_and_ignores_case()
    {
        Assert.Equal("GBP", CurrencyChoice.Resolve("gbp", German, Rates()).Code);
        Assert.Equal("USD", CurrencyChoice.Resolve("USD", German, Rates()).Code);
    }

    [Fact]
    public void Without_a_rate_amounts_stay_in_dollars()
    {
        Assert.Equal("USD", CurrencyChoice.Resolve(CurrencyChoice.Automatic, German, null).Code);
        Assert.Equal("USD", CurrencyChoice.Resolve(CurrencyChoice.Automatic, German, new CurrencyRatesCache()).Code);
        Assert.Equal("USD", CurrencyChoice.Resolve("XYZ", German, Rates()).Code);
        Assert.Equal("USD", CurrencyChoice.Resolve(null, CultureInfo.InvariantCulture, Rates()).Code);
    }

    [Fact]
    public void The_choice_list_has_dollars_first_then_the_rest_by_name()
    {
        var options = CurrencyChoice.Options(Rates(), German);

        Assert.Equal("auto", options[0].Code);
        Assert.Contains("(EUR)", options[0].Label); // German region, and there is a rate for it
        Assert.Equal("USD", options[1].Code);
        Assert.Equal("US Dollar (USD)", options[1].Label, ignoreCase: true);
        Assert.Equal(new[] { "EUR", "GBP", "JPY", "SEK" }, options.Skip(2).Select(o => o.Code).Order());
        Assert.All(options, o => Assert.False(string.IsNullOrWhiteSpace(o.Name)));
    }

    [Fact]
    public void Rates_are_refreshed_twice_a_day_and_when_there_are_none()
    {
        var rates = Rates();
        var fetched = rates.FetchedAt;

        Assert.False(rates.IsStale(fetched.AddHours(11)));
        Assert.True(rates.IsStale(fetched.AddHours(12)));
        Assert.True(new CurrencyRatesCache().IsStale(fetched));
    }
}
