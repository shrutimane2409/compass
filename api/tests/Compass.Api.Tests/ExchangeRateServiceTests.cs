using Compass.Api.Endpoints;
using Compass.Api.Infrastructure;

namespace Compass.Api.Tests;

public class ExchangeRateServiceTests
{
    private const string Feed = """
        <?xml version="1.0" encoding="UTF-8"?>
        <gesmes:Envelope xmlns:gesmes="http://www.gesmes.org/xml/2002-08-01" xmlns="http://www.ecb.int/vocabulary/2002-08-01/eurofxref">
          <gesmes:subject>Reference rates</gesmes:subject>
          <Cube>
            <Cube time="2026-11-26">
              <Cube currency="USD" rate="1.0812"/>
              <Cube currency="INR" rate="92.4150"/>
            </Cube>
          </Cube>
        </gesmes:Envelope>
        """;

    [Fact]
    public void Parses_rate_and_publication_date()
    {
        var inr = ExchangeRateService.Parse(Feed)["INR"];
        Assert.Equal(92.4150m, inr.PerEur);
        Assert.Equal(new DateOnly(2026, 11, 26), inr.Date);
        Assert.Equal(ExchangeRateService.FeedUrl, inr.SourceUrl);
    }

    [Fact]
    public void Feedback_rejects_unknown_fields_and_empty_messages()
    {
        var errors = new FeedbackRequest(1, "colour", " ", null).Validate();
        Assert.Contains("field", errors.Keys);
        Assert.Contains("message", errors.Keys);
        Assert.Empty(new FeedbackRequest(3, "fee", "The 2027/28 fee is now €30,000.", null).Validate());
    }
}
