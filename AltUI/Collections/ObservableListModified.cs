using System;
using System.Collections.Generic;

namespace AltUI.Collections;

public class ObservableListModified<T>(IEnumerable<T> items) : EventArgs
{
    public IEnumerable<T> Items { get; private set; } = items;
}
