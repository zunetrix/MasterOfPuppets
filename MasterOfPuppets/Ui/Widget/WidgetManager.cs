using System;
using System.Collections.Generic;

namespace MasterOfPuppets;

public sealed class WidgetManager {
    private readonly List<LazyWidget> _widgets = new();
    // private readonly List<Widget> _widgets = new();
    private Widget? _current;
    private bool _initialized;

    public IReadOnlyList<LazyWidget> Widgets => _widgets;

    public void Add(Func<Widget> factory) {
        _widgets.Add(new LazyWidget(factory));
    }

    // public void Add(Widget widget) {
    //     _widgets.Add(widget);
    // }

    public void Show(int index) {
        if (index < 0 || index >= _widgets.Count)
            return;

        var widget = _widgets[index].Instance;

        if (_current == widget)
            return;

        _current?.Hide();
        _current = widget;
        _current.Show();
    }

    // public void Show(Widget widget) {
    //     if (_current == widget)
    //         return;

    //     _current?.Hide();
    //     _current = widget;
    //     _current.Show();
    // }

    public void Draw() {
        // safe lazy init
        if (!_initialized) {
            _initialized = true;

            if (_current == null && _widgets.Count > 0) {
                this.Show(0);
            }
        }

        _current?.DrawInternal();
    }
}

