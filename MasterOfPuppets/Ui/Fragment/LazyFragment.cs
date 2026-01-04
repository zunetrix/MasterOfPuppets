using System;

namespace MasterOfPuppets;

public sealed class LazyFragment {
    private readonly Func<Fragment> _factory;
    private Fragment? _instance;

    public Fragment Instance => _instance ??= _factory();

    public LazyFragment(Func<Fragment> factory) {
        _factory = factory;
    }

    public void Dispose() {
        _instance = null;
    }
}
