using System;

namespace MasterOfPuppets;

public sealed class LazyWidget {
    private readonly Func<Widget> _factory;
    private Widget? _instance;

    public Widget Instance => _instance ??= _factory();

    public LazyWidget(Func<Widget> factory) {
        _factory = factory;
    }

    public void Dispose() {
        _instance = null;
    }
}
