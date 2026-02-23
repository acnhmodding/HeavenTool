using System.ComponentModel;

namespace HeavenTool.Utility.UndoSystem;

public class PropertyChangeUndoCommand(object target, PropertyDescriptor property, object? oldValue, object? newValue) : IUndoCommand
{
    private readonly object _target = target;
    private readonly PropertyDescriptor _property = property;
    private readonly object? _oldValue = oldValue;
    private readonly object? _newValue = newValue;

    public void Undo()
    {
        _property.SetValue(_target, _oldValue);
    }

    public void Redo()
    {
        _property.SetValue(_target, _newValue);
    }
}