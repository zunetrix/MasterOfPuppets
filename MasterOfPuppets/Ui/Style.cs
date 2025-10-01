using System.Numerics;

namespace MasterOfPuppets;

// constant components/colors that are not customizable
public static class Style {
    public static ColorPalette Colors = new();
    public static ComponentsPalette Components = new();
}

public class ComponentsPalette {
    // imgui
    public Vector4 Text = new Vector4(1f, 1f, 1f, 1f);                         // #FFFFFF
    public Vector4 TextDisabled = new Vector4(0.5f, 0.5f, 0.5f, 1f);           // #808080
    public Vector4 TextSelectedBg = new Vector4(0.26f, 0.59f, 0.98f, 0.35f);   // #4195F2
    public Vector4 WindowBg = new Vector4(0.06f, 0.06f, 0.06f, 0.93f);         // #0F0F0FEC
    public Vector4 MenuBarBg = new Vector4(0.14f, 0.14f, 0.14f, 1f);           // #232323
    public Vector4 ChildBg = new Vector4(0f, 0f, 0f, 0f);                     // #00000000
    public Vector4 PopupBg = new Vector4(0.08f, 0.08f, 0.08f, 0.94f);          // #141414F0
    public Vector4 Border = new Vector4(0.43f, 0.43f, 0.5f, 0.5f);             // #6E6E80
    public Vector4 BorderShadow = new Vector4(0f, 0f, 0f, 0f);                 // #00000000
    public Vector4 FrameBg = new Vector4(0.29f, 0.29f, 0.29f, 0.54f);          // #4A4A4A8A
    public Vector4 FrameBgHovered = new Vector4(0.54f, 0.54f, 0.54f, 0.4f);     // #87878766
    public Vector4 FrameBgActive = new Vector4(0.64f, 0.64f, 0.64f, 0.67f);     // #A3A3A8AB
    public Vector4 TitleBg = new Vector4(0.022624433f, 0.022624206f, 0.022624206f, 0.85067874f);  // #060606D8
    public Vector4 TitleBgActive = new Vector4(0.38914025f, 0.10917056f, 0.10917056f, 0.8280543f);  // #639F1BDB
    public Vector4 TitleBgCollapsed = new Vector4(0f, 0f, 0f, 0.51f);           // #00000082
    public Vector4 ScrollbarBg = new Vector4(0f, 0f, 0f, 0f);                  // #00000000
    public Vector4 ScrollbarGrab = new Vector4(0.31f, 0.31f, 0.31f, 1f);        // #4F4F4F
    public Vector4 ScrollbarGrabHovered = new Vector4(0.41f, 0.41f, 0.41f, 1f);  // #696969
    public Vector4 ScrollbarGrabActive = new Vector4(0.51f, 0.51f, 0.51f, 1f);  // #828282
    public Vector4 CheckMark = new Vector4(0.86f, 0.86f, 0.86f, 1f);            // #DBDBDB
    public Vector4 SliderGrab = new Vector4(0.54f, 0.54f, 0.54f, 1f);           // #8A8A8A
    public Vector4 SliderGrabActive = new Vector4(0.67f, 0.67f, 0.67f, 1f);     // #A8A8A8
    public Vector4 Button = new Vector4(0.71f, 0.71f, 0.71f, 0.4f);            // #B4B4B4
    public Vector4 ButtonHovered = new Vector4(0.3647059f, 0.078431375f, 0.078431375f, 0.94509804f);  // #5D1414F2
    public Vector4 ButtonActive = new Vector4(0.48416287f, 0.10077597f, 0.10077597f, 0.94509804f); // #7C1A1AF2
    public Vector4 Header = new Vector4(0.59f, 0.59f, 0.59f, 0.31f);            // #9494944F
    public Vector4 HeaderHovered = new Vector4(0.5f, 0.5f, 0.5f, 0.8f);          // #808080CC
    public Vector4 HeaderActive = new Vector4(0.6f, 0.6f, 0.6f, 1f);            // #999999
    public Vector4 Separator = new Vector4(0.43f, 0.43f, 0.5f, 0.5f);           // #6E6E80
    public Vector4 SeparatorHovered = new Vector4(0.3647059f, 0.078431375f, 0.078431375f, 0.78280544f);  // #5D1414C7
    public Vector4 SeparatorActive = new Vector4(0.3647059f, 0.078431375f, 0.078431375f, 0.94509804f);  // #5D1414F2
    public Vector4 ResizeGrip = new Vector4(0.79f, 0.79f, 0.79f, 0.25f);         // #CACACA40
    public Vector4 ResizeGripHovered = new Vector4(0.78f, 0.78f, 0.78f, 0.67f);   // #C8C8C8AB
    public Vector4 ResizeGripActive = new Vector4(0.3647059f, 0.078431375f, 0.078431375f, 0.94509804f);  // #5D1414F2
    public Vector4 Tab = new Vector4(0.23f, 0.23f, 0.23f, 0.86f);               // #3A3A3AF7
    public Vector4 TabHovered = new Vector4(0.58371043f, 0.30374074f, 0.30374074f, 0.7647059f);  // #94504C
    public Vector4 TabActive = new Vector4(0.47963798f, 0.15843244f, 0.15843244f, 0.7647059f);  // #7A2828
    public Vector4 TabUnfocused = new Vector4(0.068f, 0.10199998f, 0.14800003f, 0.9724f);       // #112648
    public Vector4 TabUnfocusedActive = new Vector4(0.13599998f, 0.26199996f, 0.424f, 1f);         // #227A6B
    public Vector4 DockingPreview = new Vector4(0.26f, 0.59f, 0.98f, 0.7f);                      // #4195F2B3
    public Vector4 DockingEmptyBg = new Vector4(0.2f, 0.2f, 0.2f, 1f);                          // #333333
    public Vector4 PlotLines = new Vector4(0.61f, 0.61f, 0.61f, 1f);                            // #9B9B9B
    public Vector4 PlotLinesHovered = new Vector4(1f, 0.43f, 0.35f, 1f);                        // #FF6F59
    public Vector4 PlotHistogram = new Vector4(0.9f, 0.7f, 0f, 1f);                             // #E5B200
    public Vector4 PlotHistogramHovered = new Vector4(1f, 0.6f, 0f, 1f);                         // #FF9900
    public Vector4 TableHeaderBg = new Vector4(0.19f, 0.19f, 0.2f, 1f);                         // #303030
    public Vector4 TableBorderStrong = new Vector4(0.31f, 0.31f, 0.35f, 1f);                     // #4F4F59
    public Vector4 TableBorderLight = new Vector4(0.23f, 0.23f, 0.25f, 1f);                      // #3A3A40
    public Vector4 TableRowBg = new Vector4(0f, 0f, 0f, 0f);                                    // #00000000
    public Vector4 TableRowBgAlt = new Vector4(1f, 1f, 1f, 0.06f);                              // #FFFFFF0F
    public Vector4 NavHighlight = new Vector4(0.26f, 0.59f, 0.98f, 1f);                         // #4195F2
    public Vector4 NavWindowingHighlight = new Vector4(1f, 1f, 1f, 0.7f);                        // #FFFFFFB3
    public Vector4 NavWindowingDimBg = new Vector4(0.8f, 0.8f, 0.8f, 0.2f);                      // #CCCCCC33
    public Vector4 DragDropTarget = new Vector4(1f, 1f, 0f, 0.9f);                              // #FFFF00E6
    public Vector4 ModalWindowDimBg = new Vector4(0.8f, 0.8f, 0.8f, 0.35f);                      // #CCCCCC59

    // custom
    public Vector4 ButtonSuccessnNormal = new Vector4(0.342857f, 0.6f, 0.24f, 1f);   // #57CC3D
    public Vector4 ButtonSuccessHovered = new Vector4(0.35f, 0.7f, 0.21f, 1f);       // #59B335
    public Vector4 ButtonSuccessActive = new Vector4(0.342857f, 0.8f, 0.16f, 1f);    // #57CC29

    public Vector4 ButtonDangerNormal = new Vector4(0.6f, 0.24f, 0.24f, 1f);          // #993D3D
    public Vector4 ButtonDangerHovered = new Vector4(0.7f, 0.21f, 0.21f, 1f);        // #B33535
    public Vector4 ButtonDangerActive = new Vector4(0.8f, 0.16f, 0.16f, 1f);         // #CC2929

    public Vector4 ButtonInfoNormal = new Vector4(0.26f, 0.59f, 0.98f, 0.40f);       // #4296F966
    public Vector4 ButtonInfoHovered = new Vector4(0.26f, 0.59f, 0.98f, 1.00f);      // #4296F9
    public Vector4 ButtonInfoActive = new Vector4(0.06f, 0.53f, 0.98f, 1.00f);       // #0F87F9

    public Vector4 ButtonBlueNormal = new Vector4(0.24f, 0.445714f, 0.6f, 1f);       // #3D7299
    public Vector4 ButtonBlueHovered = new Vector4(0.21f, 0.49f, 0.7f, 1f);          // #356FB3
    public Vector4 ButtonBlueActive = new Vector4(0.16f, 0.525714f, 0.8f, 1f);       // #2999CC

    public Vector4 ButtonPurpleNormal = new Vector4(0.36f, 0.36f, 0.6f, 1f);  // #5C5CCC
    public Vector4 ButtonPurpleHovered = new Vector4(0.42f, 0.42f, 0.7f, 1f);  // #6B6BCC
    public Vector4 ButtonPurpleActive = new Vector4(0.48f, 0.48f, 0.8f, 1f);  // #7A7ACC

    public Vector4 ButtonPinkNormal = new Vector4(0.6f, 0.24f, 0.6f, 1f);          // #993D99
    public Vector4 ButtonPinkHovered = new Vector4(0.7f, 0.21f, 0.7f, 1f);        // #B335B3
    public Vector4 ButtonPinkActive = new Vector4(0.8f, 0.16f, 0.8f, 1f);         // #CC29CC


    public Vector4 ButtonDiscordNormal = new Vector4(0.34509805f, 0.39607847f, 0.9490197f, 1f);     // #5865F2
    public Vector4 ButtonDiscordActive = new Vector4(0.34509805f, 0.39607847f, 0.9490197f, 0.86666673f); // #5865F2DD
    public Vector4 ButtonDiscordHovered = new Vector4(0.34509805f, 0.39607847f, 0.9490197f, 0.6666667f); // #5865F2AA

    public Vector4 ButtonWebsiteNormal = new Vector4(0.08627451f, 0.6431373f, 0.7803922f, 1f);               // #16A4C7
    public Vector4 ButtonWebsiteActive = new Vector4(0.08627451f, 0.6431373f, 0.7803922f, 0.86666673f);      // #16A4C7DD
    public Vector4 ButtonWebsiteHovered = new Vector4(0.08627451f, 0.6431373f, 0.7803922f, 0.6666667f);      // #16A4C7AA

    public Vector4 TooltipBorderColor = new Vector4(1.0f, 0.64705884f, 0.0f, 1.0f);  // #FFA500
}

public class ColorPalette {
    // public Vector4 Red = new Vector4(1f, 0.2f, 0.2f, 1f);
    // public Vector4 Red = new Vector4(0.784f, 0f, 0f, 1f);
    // public Vector4 Red = new Vector4(1f, 0.35686275f, 0.36862746f, 1f);

    public Vector4 Transparent = new Vector4(1f, 1f, 1f, 0.1f);
    public Vector4 Red = new Vector4(0.81568635f, 0f, 0f, 0.6666667f);
    public Vector4 Green = new Vector4(0.2f, 1f, 0.2f, 1f);
    public Vector4 Blue = new Vector4(0.2f, 0.6f, 1f, 1f);
    public Vector4 Violet = new Vector4(0.5568628f, 0.53333336f, 1f, 0.6666667f);
    // public Vector4 Violet = new Vector4(0.7f, 0.5f, 0.9f, 1f);
    // public Vector4 Orange = new Vector4(1f, 0.5f, 0.9f, 1f);

    public Vector4 Orange = new Vector4(1f, 0.6f, 0.2f, 1f);
    public Vector4 Yellow = new Vector4(0.7843138f, 0.7843138f, 0f, 1f);
    public Vector4 Lavender = new Vector4(0.65882355f, 0.65882355f, 1f, 1f);
    // public Vector4 Yellow = new Vector4(1f, 1f, 0.4f, 1f);
    public Vector4 Cyan = new Vector4(0.0f, 0.9804f, 1.0f, 1.0f);
    // public Vector4 Cyan = new Vector4(0.4f, 1f, 1f, 1f);
    public Vector4 Gray = new Vector4(0.5f, 0.5f, 0.5f, 1f);
    public Vector4 White = new Vector4(1f, 1f, 1f, 1f);
    public Vector4 Black = new Vector4(0f, 0f, 0f, 1f);

    public Vector4 GrassGreen = new Vector4(0.5568628f, 1f, 0.37647063f, 0.6117647f);
    public Vector4 GrassGreen50 = new Vector4(0.5568628f, 1f, 0.37647063f, 0.23529413f);
    public Vector4 DarkGreen = new Vector4(0.1254902f, 0.2509804f, 0.0627451f, 0.6745098f);
    public Vector4 DarkGray = new Vector4(0.18f, 0.21f, 0.25f, 1f);

    public static Vector4 AutoTranslateStartGreen = new(
        96.0f / 255.0f,
        223.0f / 255.0f,
        46.0f / 255.0f,
        1.0f
    ); // #60DF2E

    public static Vector4 AutoTranslateEndRed = new(
        221 / 255.0f,
        54 / 255.0f,
        54 / 255.0f,
        1.0f
    ); // #DD3636
}

public static class ColorUtil {
    public static Vector4 HexToVector4(string hex, float alpha = 1.0f) {
        if (hex.StartsWith("#"))
            hex = hex.Substring(1);

        if (hex.Length != 6)
            return new Vector4();

        var r = int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
        var g = int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
        var b = int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255f;

        return new Vector4(r, g, b, alpha);
    }

    public static uint Vector4ToUint(Vector4 color) {
        var r = (uint)(color.X * 255.0f);
        var g = (uint)(color.Y * 255.0f);
        var b = (uint)(color.Z * 255.0f);
        var a = (uint)(color.W * 255.0f);

        // ImGui usa formato ABGR
        return (a << 24) | (b << 16) | (g << 8) | r;
    }
}
