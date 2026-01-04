namespace MasterOfPuppets;

public interface IFragment {
    string Title { get; }
    void Render(FragmentContext ctx);
}
