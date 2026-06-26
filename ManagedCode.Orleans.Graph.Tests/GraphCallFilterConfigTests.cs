using ManagedCode.Orleans.Graph.Models;

namespace ManagedCode.Orleans.Graph.Tests;

public class GraphCallFilterConfigTests
{
    [Test]
    public void Defaults_DoNotTrackOrleansOrOrleansGraphInternalCalls()
    {
        var config = new GraphCallFilterConfig();

        config.TrackOrleansCalls.ShouldBeFalse();
        config.TrackOrleansGraphInternalCalls.ShouldBeFalse();
        config.LiveGraphFlushPeriod.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Test]
    public void TrackOrleansGraphInternalCalls_CanBeEnabledExplicitly()
    {
        var config = new GraphCallFilterConfig
        {
            TrackOrleansGraphInternalCalls = true
        };

        config.TrackOrleansGraphInternalCalls.ShouldBeTrue();
    }
}
