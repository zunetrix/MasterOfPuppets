using System.Numerics;

namespace MasterOfPuppets.Extensions;

public static class VectorExtensions {

    public static uint ToUintColor(this Vector4 color) {
        var r = (uint)(color.X * 255.0f);
        var g = (uint)(color.Y * 255.0f);
        var b = (uint)(color.Z * 255.0f);
        var a = (uint)(color.W * 255.0f);

        // ImGui ABGR
        return (a << 24) | (b << 16) | (g << 8) | r;
    }
}
