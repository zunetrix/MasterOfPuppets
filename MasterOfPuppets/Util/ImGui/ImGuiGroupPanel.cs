using System;
using System.Collections.Generic;
using System.Numerics;

using Dalamud.Bindings.ImGui;

namespace MasterOfPuppets;

// https://github.com/ocornut/imgui/issues/1496#issuecomment-655048353
public static class ImGuiGroupPanel
{
    static readonly Stack<RectF> s_GroupPanelLabelStack = new Stack<RectF>();

    public static unsafe void BeginGroupPanel(string name)
    {
        BeginGroupPanel(name, -Vector2.One);
    }

    public static unsafe void BeginGroupPanel(string name, Vector2 size)
    {
        ImGui.BeginGroup();
        var cursorPos = ImGui.GetCursorScreenPos();
        var itemSpacing = ImGui.GetStyle().ItemSpacing;
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0.0f, 0.0f));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0.0f, 0.0f));

        var frameHeight = ImGui.GetFrameHeight();
        ImGui.BeginGroup();

        Vector2 effectiveSize = size;
        if (size.X < 0.0f)
            effectiveSize.X = ImGuiUtil.GetWindowContentRegionWidth();
        else
            effectiveSize.X = size.X;
        ImGui.Dummy(new Vector2(effectiveSize.X, 0.0f));

        ImGui.Dummy(new Vector2(frameHeight * 0.5f, 0.0f));
        ImGui.SameLine(0.0f, 0.0f);
        ImGui.BeginGroup();
        ImGui.Dummy(new Vector2(frameHeight * 0.5f, 0.0f));
        ImGui.SameLine(0.0f, 0.0f);
        ImGui.TextUnformatted(name);
        var labelMin = ImGui.GetItemRectMin();
        var labelMax = ImGui.GetItemRectMax();
        ImGui.SameLine(0.0f, 0.0f);
        ImGui.Dummy(new Vector2(0.0f, frameHeight + itemSpacing.Y));
        ImGui.BeginGroup();

        //ImGui.GetWindowDrawList()->AddRect(labelMin, labelMax, IM_COL32(255, 0, 255, 255));

        ImGui.PopStyleVar(2);

        igGetContentRegionMax()->X -= frameHeight * 0.5f;
        igGetWindowSize()->X -= frameHeight;

        var itemWidth = ImGui.CalcItemWidth();
        ImGui.PushItemWidth(Math.Max(0.0f, itemWidth - frameHeight));

        s_GroupPanelLabelStack.Push(new RectF(labelMin, labelMax));

        ImGui.PushTextWrapPos(igGetContentRegionMax()->X);
    }

    public static unsafe void EndGroupPanel()
    {
        ImGui.PopTextWrapPos();
        ImGui.PopItemWidth();

        var itemSpacing = ImGui.GetStyle().ItemSpacing;

        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0.0f, 0.0f));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0.0f, 0.0f));

        var frameHeight = ImGui.GetFrameHeight();

        ImGui.EndGroup();

        //ImGui.GetWindowDrawList()->AddRectFilled(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), IM_COL32(0, 255, 0, 64), 4.0f);

        ImGui.EndGroup();

        ImGui.SameLine(0.0f, 0.0f);
        ImGui.Dummy(new Vector2(frameHeight * 0.5f, 0.0f));
        ImGui.Dummy(new Vector2(0.0f, frameHeight - frameHeight * 0.5f - itemSpacing.Y));

        ImGui.EndGroup();

        var itemMin = ImGui.GetItemRectMin();
        var itemMax = ImGui.GetItemRectMax();
        //ImGui.GetWindowDrawList()->AddRectFilled(itemMin, itemMax, IM_COL32(255, 0, 0, 64), 4.0f);

        var labelRect = s_GroupPanelLabelStack.Pop();

        var halfFrame = new Vector2(frameHeight * 0.25f, frameHeight) * 0.5f;
        var frameRect = new RectF(itemMin + halfFrame, itemMax - new Vector2(halfFrame.X, 0.0f));
        labelRect.Min.X -= itemSpacing.X;
        labelRect.Max.X += itemSpacing.X;
        for (int i = 0; i < 4; ++i)
        {
            const float FLT_MAX = float.MaxValue;
            switch (i)
            {
                // left half-plane
                case 0: ImGui.PushClipRect(new Vector2(-FLT_MAX, -FLT_MAX), new Vector2(labelRect.Min.X, FLT_MAX), true); break;
                // right half-plane
                case 1: ImGui.PushClipRect(new Vector2(labelRect.Max.X, -FLT_MAX), new Vector2(FLT_MAX, FLT_MAX), true); break;
                // top
                case 2: ImGui.PushClipRect(new Vector2(labelRect.Min.X, -FLT_MAX), new Vector2(labelRect.Max.X, labelRect.Min.Y), true); break;
                // bottom
                case 3: ImGui.PushClipRect(new Vector2(labelRect.Min.X, labelRect.Max.Y), new Vector2(labelRect.Max.X, FLT_MAX), true); break;
            }

            ImGui.GetWindowDrawList().AddRect(
                frameRect.Min, frameRect.Max,
                ImGui.GetColorU32(ImGuiCol.Border),
                halfFrame.X);

            ImGui.PopClipRect();
        }

        ImGui.PopStyleVar(2);

        igGetContentRegionMax()->X += frameHeight * 0.5f;
        igGetWindowSize()->X += frameHeight;

        ImGui.Dummy(new Vector2(0.0f, 0.0f));

        ImGui.EndGroup();
    }

    private static unsafe Vector2* igGetWindowSize()
    {
        Vector2 v;
        ImGuiNative.GetWindowSize(&v);
        return &v;
    }

    private static unsafe Vector2* igGetContentRegionMax()
    {
        Vector2 v;
        ImGuiNative.GetContentRegionMax(&v);
        return &v;
    }

    struct RectF
    {
        public RectF(Vector2 min, Vector2 max) : this()
        {
            Min = min;
            Max = max;
        }

        public Vector2 Min;
        public Vector2 Max;
    }
}

