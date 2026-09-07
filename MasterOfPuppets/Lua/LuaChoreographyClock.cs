using System;

using Framework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace MasterOfPuppets.LuaScripting;

public static unsafe class LuaChoreographyClock {
    public static long GetServerTimeSeconds() {
        try {
            var value = Framework.GetServerTime();
            if (value > 0)
                return value;
        } catch {
            // Fall through only when the native framework is unavailable,
            // such as during shutdown or isolated tests.
        }
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
