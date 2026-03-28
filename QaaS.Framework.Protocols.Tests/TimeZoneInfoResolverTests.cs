using QaaS.Framework.Infrastructure;

namespace QaaS.Framework.Protocols.Tests;

[TestFixture]
public class TimeZoneInfoResolverTests
{
    [Test]
    public void ResolveTimeZoneInfo_WhenDefaultRequested_DoesNotThrow()
    {
        Assert.That(() => TimeZoneInfoResolver.ResolveTimeZoneInfo(), Throws.Nothing);
    }

    [Test]
    public void ResolveTimeZoneInfo_WhenWindowsTimeZoneIdProvided_ResolvesAcrossPlatforms()
    {
        var timeZoneInfo = TimeZoneInfoResolver.ResolveTimeZoneInfo(TimeZoneInfoResolver.DefaultWindowsTimeZoneId);

        Assert.That(timeZoneInfo.Id, Is.Not.Empty);
    }
}
