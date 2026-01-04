using System;
using System.Collections.Generic;

namespace MasterOfPuppets;

public sealed class FragmentManager {
    private readonly List<LazyFragment> _fragments = new();
    private Fragment? _current;

    public IReadOnlyList<LazyFragment> Fragments => _fragments;

    public void Add(Func<Fragment> factory) {
        _fragments.Add(new LazyFragment(factory));
    }

    // public void Add(Fragment fragment) {
    //     _fragments.Add(fragment);
    // }

    public void Show(int index) {
        var fragment = _fragments[index].Instance;

        if (_current == fragment)
            return;

        _current?.Hide();
        _current = fragment;
        _current.Show();
    }

    // public void Show(Fragment fragment) {
    //     if (_current == fragment)
    //         return;

    //     _current?.Hide();
    //     _current = fragment;
    //     _current.Show();
    // }

    public void Draw() {
        _current?.DrawInternal();
    }
}
