using HeavenTool.Utility;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace HeavenTool.Forms.Editor;

public class BaseEditor : DockContent
{
    public UndoManager UndoManager { get; } = new();

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string FilePath { get; set; } = "";


    public bool _isDirty;
    public bool IsDirty { 
        get => _isDirty;
        private set {
            _isDirty = value;
            DockHandler.TabText = Text + (_isDirty ? "*" : "");
        } 
    }

    public void SetDirty(bool value) => IsDirty = value;

    public virtual void LoadFile(Stream stream)
    {

    }

    public virtual void SaveFile()
    {

    }


    #region Optional Methods
    public virtual void BuildContextMenu(ContextMenuStrip contextMenu)
    {

    }
    #endregion

    #region Common Methods
    public void Undo()
    {
        UndoManager.Undo();

        if (UndoManager.GetUndoCount() == 0)
            SetDirty(false);
        
        Invalidate();
    }

    public void Redo()
    {
        UndoManager.Redo();

        if (UndoManager.GetRedoCount() == 0)
            SetDirty(false);
        
        Invalidate();
    }
    #endregion
}
