namespace HeavenTool.Forms.BFRES;

partial class BFRESViewer
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing) { DisposeDockWindows(); components?.Dispose(); }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code
    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        menuStrip1 = new System.Windows.Forms.MenuStrip();
        dockPanel1 = new WeifenLuo.WinFormsUI.Docking.DockPanel();
        statusStrip = new System.Windows.Forms.StatusStrip();
        status = new System.Windows.Forms.ToolStripStatusLabel();
        fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
        viewMenuItem = new System.Windows.Forms.ToolStripMenuItem();
        lightingMenuItem = new System.Windows.Forms.ToolStripMenuItem();
        exposureItem = new System.Windows.Forms.ToolStripMenuItem();
        openToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
        fileSeparator = new System.Windows.Forms.ToolStripSeparator();
        loadShaderItem = new System.Windows.Forms.ToolStripMenuItem();
        frameAllItem = new System.Windows.Forms.ToolStripMenuItem();
        wireframeItem = new System.Windows.Forms.ToolStripMenuItem();
        viewSeparator = new System.Windows.Forms.ToolStripSeparator();
        showBonesItem = new System.Windows.Forms.ToolStripMenuItem();
        frameBoneItem = new System.Windows.Forms.ToolStripMenuItem();
        windowSeparator = new System.Windows.Forms.ToolStripSeparator();
        scenePaneItem = new System.Windows.Forms.ToolStripMenuItem();
        materialPaneItem = new System.Windows.Forms.ToolStripMenuItem();
        restoreLayoutItem = new System.Windows.Forms.ToolStripMenuItem();
        dayLightingItem = new System.Windows.Forms.ToolStripMenuItem();
        nightLightingItem = new System.Windows.Forms.ToolStripMenuItem();
        lightingSeparator = new System.Windows.Forms.ToolStripSeparator();
        skyFillItem = new System.Windows.Forms.ToolStripMenuItem();
        restoreLightingItem = new System.Windows.Forms.ToolStripMenuItem();
        darkerItem = new System.Windows.Forms.ToolStripMenuItem();
        resetExposureItem = new System.Windows.Forms.ToolStripMenuItem();
        brighterItem = new System.Windows.Forms.ToolStripMenuItem();
        menuStrip1.SuspendLayout();
        statusStrip.SuspendLayout();
        SuspendLayout();
        // menuStrip1
        menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { fileToolStripMenuItem, viewMenuItem, lightingMenuItem });
        menuStrip1.Location = new System.Drawing.Point(0, 0);
        menuStrip1.Name = "menuStrip1";
        menuStrip1.Size = new System.Drawing.Size(1440, 24);
        // fileToolStripMenuItem
        fileToolStripMenuItem.Name = "fileToolStripMenuItem";
        fileToolStripMenuItem.Text = "File";
        fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { openToolStripMenuItem, fileSeparator, loadShaderItem });
        // viewMenuItem
        viewMenuItem.Name = "viewMenuItem";
        viewMenuItem.Text = "View";
        viewMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { frameAllItem, wireframeItem, viewSeparator, showBonesItem, frameBoneItem, windowSeparator, scenePaneItem, materialPaneItem, restoreLayoutItem });
        // lightingMenuItem
        lightingMenuItem.Name = "lightingMenuItem";
        lightingMenuItem.Text = "Lighting";
        lightingMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { dayLightingItem, nightLightingItem, lightingSeparator, skyFillItem, exposureItem, restoreLightingItem });
        // exposureItem
        exposureItem.Name = "exposureItem";
        exposureItem.Text = "Exposure: 0 EV";
        exposureItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { darkerItem, resetExposureItem, brighterItem });
        // openToolStripMenuItem
        openToolStripMenuItem.Name = "openToolStripMenuItem";
        openToolStripMenuItem.Text = "Open...";
        // fileSeparator
        fileSeparator.Name = "fileSeparator";
        // loadShaderItem
        loadShaderItem.Name = "loadShaderItem";
        loadShaderItem.Text = "Load game materials...";
        // frameAllItem
        frameAllItem.Name = "frameAllItem";
        frameAllItem.Text = "Frame all";
        // wireframeItem
        wireframeItem.Name = "wireframeItem";
        wireframeItem.Text = "Wireframe";
        wireframeItem.CheckOnClick = true;
        // viewSeparator
        viewSeparator.Name = "viewSeparator";
        // showBonesItem
        showBonesItem.Name = "showBonesItem";
        showBonesItem.Text = "Show bones";
        showBonesItem.CheckOnClick = true;
        // frameBoneItem
        frameBoneItem.Name = "frameBoneItem";
        frameBoneItem.Text = "Frame selected bone";
        // windowSeparator
        windowSeparator.Name = "windowSeparator";
        // scenePaneItem
        scenePaneItem.Name = "scenePaneItem";
        scenePaneItem.Text = "Scene explorer";
        // materialPaneItem
        materialPaneItem.Name = "materialPaneItem";
        materialPaneItem.Text = "Material properties...";
        // restoreLayoutItem
        restoreLayoutItem.Name = "restoreLayoutItem";
        restoreLayoutItem.Text = "Restore default layout";
        // dayLightingItem
        dayLightingItem.Name = "dayLightingItem";
        dayLightingItem.Text = "Day";
        dayLightingItem.Checked = true;
        // nightLightingItem
        nightLightingItem.Name = "nightLightingItem";
        nightLightingItem.Text = "Night";
        // lightingSeparator
        lightingSeparator.Name = "lightingSeparator";
        // skyFillItem
        skyFillItem.Name = "skyFillItem";
        skyFillItem.Text = "Ambient light";
        skyFillItem.CheckOnClick = true;
        skyFillItem.Checked = true;
        // restoreLightingItem
        restoreLightingItem.Name = "restoreLightingItem";
        restoreLightingItem.Text = "Reset lighting";
        // darkerItem
        darkerItem.Name = "darkerItem";
        darkerItem.Text = "Darker";
        // resetExposureItem
        resetExposureItem.Name = "resetExposureItem";
        resetExposureItem.Text = "Reset";
        // brighterItem
        brighterItem.Name = "brighterItem";
        brighterItem.Text = "Brighter";
        openToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.O;
        frameAllItem.ShortcutKeyDisplayString = "F";
        showBonesItem.ShortcutKeyDisplayString = "B";
        // dockPanel1
        dockPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
        dockPanel1.DockLeftPortion = 0.19D;
        dockPanel1.DockRightPortion = 0.31D;
        dockPanel1.DocumentStyle = WeifenLuo.WinFormsUI.Docking.DocumentStyle.DockingWindow;
        dockPanel1.Location = new System.Drawing.Point(0, 24);
        dockPanel1.Name = "dockPanel1";
        dockPanel1.Size = new System.Drawing.Size(1440, 804);
        dockPanel1.TabIndex = 0;
        // statusStrip
        statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { status });
        statusStrip.Location = new System.Drawing.Point(0, 828);
        statusStrip.Name = "statusStrip";
        statusStrip.Size = new System.Drawing.Size(1440, 22);
        statusStrip.SizingGrip = false;
        status.Spring = true;
        status.Name = "status";
        status.Text = "Open a model to begin.";
        status.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // BFRESViewer
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        ClientSize = new System.Drawing.Size(1440, 850);
        MinimumSize = new System.Drawing.Size(1000, 650);
        Controls.Add(dockPanel1);
        Controls.Add(statusStrip);
        Controls.Add(menuStrip1);
        MainMenuStrip = menuStrip1;
        Name = "BFRESViewer";
        Text = "BFRES Viewer";
        menuStrip1.ResumeLayout(false);
        menuStrip1.PerformLayout();
        statusStrip.ResumeLayout(false);
        statusStrip.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }
    #endregion

    private System.Windows.Forms.MenuStrip menuStrip1;
    private WeifenLuo.WinFormsUI.Docking.DockPanel dockPanel1;
    private System.Windows.Forms.StatusStrip statusStrip;
    private System.Windows.Forms.ToolStripStatusLabel status;
    private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
    private System.Windows.Forms.ToolStripMenuItem viewMenuItem;
    private System.Windows.Forms.ToolStripMenuItem lightingMenuItem;
    private System.Windows.Forms.ToolStripMenuItem exposureItem;
    private System.Windows.Forms.ToolStripMenuItem openToolStripMenuItem;
    private System.Windows.Forms.ToolStripSeparator fileSeparator;
    private System.Windows.Forms.ToolStripMenuItem loadShaderItem;
    private System.Windows.Forms.ToolStripMenuItem frameAllItem;
    private System.Windows.Forms.ToolStripMenuItem wireframeItem;
    private System.Windows.Forms.ToolStripSeparator viewSeparator;
    private System.Windows.Forms.ToolStripMenuItem showBonesItem;
    private System.Windows.Forms.ToolStripMenuItem frameBoneItem;
    private System.Windows.Forms.ToolStripSeparator windowSeparator;
    private System.Windows.Forms.ToolStripMenuItem scenePaneItem;
    private System.Windows.Forms.ToolStripMenuItem materialPaneItem;
    private System.Windows.Forms.ToolStripMenuItem restoreLayoutItem;
    private System.Windows.Forms.ToolStripMenuItem dayLightingItem;
    private System.Windows.Forms.ToolStripMenuItem nightLightingItem;
    private System.Windows.Forms.ToolStripSeparator lightingSeparator;
    private System.Windows.Forms.ToolStripMenuItem skyFillItem;
    private System.Windows.Forms.ToolStripMenuItem restoreLightingItem;
    private System.Windows.Forms.ToolStripMenuItem darkerItem;
    private System.Windows.Forms.ToolStripMenuItem resetExposureItem;
    private System.Windows.Forms.ToolStripMenuItem brighterItem;
}
