using System;

using Dalamud.Interface.Utility.Raii;

namespace MasterOfPuppets.Extensions.Dalamud;

public static class ImRaiiExt {
    public static void Use(this ImRaii.IEndObject raii, Action block) {
        using (var draw = raii) {
            if (draw) {
                block();
            }
        }
    }
}
