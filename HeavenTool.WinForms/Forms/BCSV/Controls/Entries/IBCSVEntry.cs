using System;

namespace HeavenTool.Forms.BCSV.Controls.Entries;

public interface IBCSVEntry
{
    void SetCallback(Action<object> newValueCallback);
    void SetPropertyName(string name);
    void SetUniqueIdentifier();
}
