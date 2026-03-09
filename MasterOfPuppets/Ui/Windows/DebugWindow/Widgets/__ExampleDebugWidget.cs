namespace MasterOfPuppets.Debug;

public sealed class ExampleDebugWidget : Widget {
    public override string Title => "Example Debug Widget";

    public ExampleDebugWidget(WidgetContext ctx) : base(ctx) {
    }

    public override void Draw() {
        DalamudApi.PluginLog.Debug($"{Context.Plugin.Config.AllowResize}");
    }
}
