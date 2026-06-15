using System.Reflection;
using CbmEngine.Host.MonoGame;
using Xunit;

namespace CbmEngine.Tests.Unit;

/// <summary>
/// Structural guard for UP-CBM-001: MonoGameHost must compose the emulator on a single
/// code path through <see cref="CbmViewport"/> and must not retain its own duplicated
/// pump/blit/input/audio wiring.
/// </summary>
[Trait("Speed", "Fast")]
public class MonoGameHostStructureTests
{
    // TEST-CBM-HOST-009
    [Fact]
    public void TEST_CBM_HOST_009_MonoGameHost_DelegatesToSingleViewport()
    {
        var fields = typeof(MonoGameHost)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        var fieldTypes = fields.Select(f => f.FieldType).ToArray();

        Assert.Contains(typeof(CbmViewport), fieldTypes);

        Assert.DoesNotContain(typeof(EmulatorPump), fieldTypes);
        Assert.DoesNotContain(typeof(MonoGameBlitTarget), fieldTypes);
        Assert.DoesNotContain(typeof(SidPump), fieldTypes);
        Assert.DoesNotContain(typeof(MonoGameAudioBackend), fieldTypes);
    }
}
