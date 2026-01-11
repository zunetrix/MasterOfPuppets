using System.Collections.Generic;

using Dalamud.Bindings.ImGui;

namespace MasterOfPuppets.Extensions.Dalamud;

public static class ImVectorExtensions {
    public static IEnumerable<T> AsEnumerable<T>(this ImVector<T> self) where T : unmanaged {
        for (var ix = 0; ix < self.Size; ++ix) {
            yield return self[ix];
        }
    }
}
