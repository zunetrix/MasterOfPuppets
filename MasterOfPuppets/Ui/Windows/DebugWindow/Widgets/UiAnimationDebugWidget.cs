using System.Numerics;

using Dalamud.Bindings.ImGui;

namespace MasterOfPuppets.Debug;

public sealed class UiAnimationDebugWidget : Widget {
    public override string Title => "Ui Animation";

    readonly SnowEffect snowEffect = new();
    readonly HeartEffect heartEffect = new();
    readonly FireworkEffect fireworkEffect = new();

    public UiAnimationDebugWidget(WidgetContext ctx) : base(ctx) {
        snowEffect.Enabled = false;
        heartEffect.Enabled = false;
        fireworkEffect.Enabled = false;
    }

    public override void Draw() {
        DrawControls();

        float dt = ImGui.GetIO().DeltaTime;
        // var dlScreen = ImGui.GetBackgroundDrawList();
        var dl = ImGui.GetWindowDrawList();

        snowEffect.Update(dt);
        snowEffect.Draw(dl);

        heartEffect.Update(dt);
        heartEffect.Draw(dl);

        fireworkEffect.Update(dt);
        fireworkEffect.Draw(dl);
    }

    private void DrawControls() {
        CheckboxEffect("Snow", snowEffect);
        ImGui.SliderFloat("Snow Spawn", ref snowEffect.SpawnRate, 1, 100);
        ImGui.SliderFloat("Snow Speed", ref snowEffect.FallSpeed, 10, 150);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        CheckboxEffect("Hearts", heartEffect);
        ImGui.SliderFloat("Heart Spawn", ref heartEffect.SpawnRate, 1, 30);
        ImGui.SliderFloat("Heart Speed", ref heartEffect.RiseSpeed, 10, 150);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        CheckboxEffect("Fireworks", fireworkEffect);
    }

    static bool CheckboxEffect(string label, ParticleEffectBase effect) {
        bool value = effect.Enabled;
        if (ImGui.Checkbox(label, ref value)) {
            effect.Enabled = value;
            return true;
        }
        return false;
    }
}
