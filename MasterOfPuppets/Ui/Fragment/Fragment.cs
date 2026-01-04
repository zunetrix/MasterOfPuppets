using Dalamud.Interface;

namespace MasterOfPuppets;

public abstract class Fragment {
    public abstract string Title { get; }
    public virtual FontAwesomeIcon Icon => FontAwesomeIcon.None;

    protected FragmentContext Context { get; }

    internal bool IsShown { get; private set; }

    protected Fragment(FragmentContext ctx) {
        this.Context = ctx;
    }

    internal void Show() {
        if (this.IsShown)
            return;

        this.IsShown = true;
        this.OnShow();
    }

    internal void Hide() {
        if (!IsShown)
            return;

        this.IsShown = false;
        this.OnHide();
    }

    internal void DrawInternal() {
        if (this.IsShown)
            this.Draw();
    }

    public abstract void Draw();

    public virtual void OnShow() { }
    public virtual void OnHide() { }
}
