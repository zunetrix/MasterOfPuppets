using System;

using Lumina.Text;

namespace MasterOfPuppets.Util;

// from VeryImportantItem
public sealed class TextAnimator {
    // pre-computed, 36 values. converted from LCH to sRGB,
    // each value being (70, 50, i * 10) in LCH space
    private static readonly byte[,] Colors =
    {
        { 252, 131, 173 }, { 254, 132, 158 }, { 253, 134, 142 },
        { 250, 137, 128 }, { 245, 141, 115 }, { 237, 147, 103 },
        { 228, 152,  92 }, { 217, 158,  84 }, { 205, 164,  78 },
        { 191, 169,  76 }, { 176, 174,  76 }, { 161, 178,  80 },
        { 144, 182,  87 }, { 126, 186,  97 }, { 107, 189, 109 },
        {  86, 191, 122 }, {  61, 192, 137 }, {  18, 193, 153 },
        {   0, 193, 169 }, {   0, 192, 183 }, {   0, 191, 195 },
        {   0, 190, 207 }, {   0, 188, 219 }, {   0, 187, 232 },
        {   0, 185, 246 }, {  55, 182, 254 }, {  97, 178, 254 },
        { 123, 173, 254 }, { 145, 168, 254 }, { 164, 163, 254 },
        { 184, 157, 250 }, { 202, 151, 241 }, { 217, 145, 230 },
        { 230, 140, 217 }, { 240, 136, 203 }, { 247, 133, 188 }
    };

    private int _offset;
    private long _lastTick;
    private int _tickIntervalMs;

    public int SpeedMs {
        get => _tickIntervalMs;
        set => _tickIntervalMs = Math.Max(50, value);
    }

    public TextAnimator(int tickIntervalMs = 50) {
        _tickIntervalMs = Math.Max(10, tickIntervalMs);
        _lastTick = Environment.TickCount64;
    }

    public ReadOnlySpan<byte> GetAnimatedText(string text) {
        AdvanceIfNeeded();
        return Build(text);
    }

    public void Reset() {
        _offset = 0;
        _lastTick = Environment.TickCount64;
    }

    private void AdvanceIfNeeded() {
        var now = Environment.TickCount64;
        if (now - _lastTick < _tickIntervalMs)
            return;

        _offset = (_offset + 1) % Colors.GetLength(0);
        _lastTick = now;
    }

    private ReadOnlySpan<byte> Build(string text) {
        var builder = new SeStringBuilder()
            .PushColorRgba(255, 255, 255, 255);

        uint index = 0;
        var colorCount = Colors.GetLength(0);

        foreach (var c in text) {
            var colorIndex = (_offset + (index++ >> 1)) % colorCount;

            builder
                .PushEdgeColorRgba(
                    Colors[colorIndex, 0],
                    Colors[colorIndex, 1],
                    Colors[colorIndex, 2],
                    255)
                .Append(c)
                .PopEdgeColor();
        }

        return builder.PopColor().GetViewAsSpan();
    }
}
