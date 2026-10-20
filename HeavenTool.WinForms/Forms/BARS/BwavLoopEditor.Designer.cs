namespace HeavenTool.Forms.BARS
{
    partial class BwavLoopEditor
    {
        private System.ComponentModel.IContainer components = null;
        protected override void Dispose(bool disposing)
        {
            if (disposing) components?.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code
        private void InitializeComponent()
        {
            mainLayout = new System.Windows.Forms.TableLayoutPanel();
            audioInfoLabel = new System.Windows.Forms.Label();
            mixedChannelsLabel = new System.Windows.Forms.Label();
            enabled = new System.Windows.Forms.CheckBox();
            positionsLayout = new System.Windows.Forms.TableLayoutPanel();
            sampleHeader = new System.Windows.Forms.Label();
            timeHeader = new System.Windows.Forms.Label();
            startLabel = new System.Windows.Forms.Label();
            start = new System.Windows.Forms.NumericUpDown();
            startTime = new System.Windows.Forms.NumericUpDown();
            startCursor = new System.Windows.Forms.Button();
            startSeek = new System.Windows.Forms.Button();
            endLabel = new System.Windows.Forms.Label();
            end = new System.Windows.Forms.NumericUpDown();
            endTime = new System.Windows.Forms.NumericUpDown();
            endCursor = new System.Windows.Forms.Button();
            endSeek = new System.Windows.Forms.Button();
            durationLabel = new System.Windows.Forms.Label();
            statusLabel = new System.Windows.Forms.Label();
            actionsLayout = new System.Windows.Forms.FlowLayoutPanel();
            apply = new System.Windows.Forms.Button();
            reload = new System.Windows.Forms.Button();
            entire = new System.Windows.Forms.Button();
            close = new System.Windows.Forms.Button();
            mainLayout.SuspendLayout();
            positionsLayout.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)start).BeginInit();
            ((System.ComponentModel.ISupportInitialize)startTime).BeginInit();
            ((System.ComponentModel.ISupportInitialize)end).BeginInit();
            ((System.ComponentModel.ISupportInitialize)endTime).BeginInit();
            actionsLayout.SuspendLayout();
            SuspendLayout();
            // 
            // mainLayout
            // 
            mainLayout.AutoSize = true;
            mainLayout.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            mainLayout.ColumnCount = 1;
            mainLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            mainLayout.Controls.Add(audioInfoLabel, 0, 0);
            mainLayout.Controls.Add(mixedChannelsLabel, 0, 1);
            mainLayout.Controls.Add(enabled, 0, 2);
            mainLayout.Controls.Add(positionsLayout, 0, 3);
            mainLayout.Controls.Add(durationLabel, 0, 4);
            mainLayout.Controls.Add(statusLabel, 0, 5);
            mainLayout.Controls.Add(actionsLayout, 0, 6);
            mainLayout.Dock = System.Windows.Forms.DockStyle.Top;
            mainLayout.Location = new System.Drawing.Point(0, 0);
            mainLayout.Name = "mainLayout";
            mainLayout.Padding = new System.Windows.Forms.Padding(5);
            mainLayout.RowCount = 7;
            mainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
            mainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
            mainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
            mainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
            mainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
            mainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
            mainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
            mainLayout.Size = new System.Drawing.Size(615, 307);
            mainLayout.TabIndex = 0;
            // 
            // audioInfoLabel
            // 
            audioInfoLabel.AutoSize = true;
            audioInfoLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            audioInfoLabel.Location = new System.Drawing.Point(8, 8);
            audioInfoLabel.Margin = new System.Windows.Forms.Padding(3, 3, 3, 10);
            audioInfoLabel.Name = "audioInfoLabel";
            audioInfoLabel.Size = new System.Drawing.Size(599, 30);
            audioInfoLabel.TabIndex = 0;
            audioInfoLabel.Text = "Recording\nSample count, sample rate, duration and channels";
            // 
            // mixedChannelsLabel
            // 
            mixedChannelsLabel.AutoSize = true;
            mixedChannelsLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            mixedChannelsLabel.ForeColor = System.Drawing.Color.Goldenrod;
            mixedChannelsLabel.Location = new System.Drawing.Point(8, 48);
            mixedChannelsLabel.Name = "mixedChannelsLabel";
            mixedChannelsLabel.Size = new System.Drawing.Size(599, 15);
            mixedChannelsLabel.TabIndex = 1;
            mixedChannelsLabel.Text = "Channels have different loops. Apply gives every channel the same loop.";
            mixedChannelsLabel.Visible = false;
            // 
            // enabled
            // 
            enabled.AutoSize = true;
            enabled.Location = new System.Drawing.Point(8, 71);
            enabled.Margin = new System.Windows.Forms.Padding(3, 8, 3, 8);
            enabled.Name = "enabled";
            enabled.Size = new System.Drawing.Size(105, 19);
            enabled.TabIndex = 0;
            enabled.Text = "Enable looping";
            enabled.CheckedChanged += LoopValueChanged;
            // 
            // positionsLayout
            // 
            positionsLayout.AutoSize = true;
            positionsLayout.ColumnCount = 5;
            positionsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 92F));
            positionsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            positionsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            positionsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 90F));
            positionsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 72F));
            positionsLayout.Controls.Add(sampleHeader, 1, 0);
            positionsLayout.Controls.Add(timeHeader, 2, 0);
            positionsLayout.Controls.Add(startLabel, 0, 1);
            positionsLayout.Controls.Add(start, 1, 1);
            positionsLayout.Controls.Add(startTime, 2, 1);
            positionsLayout.Controls.Add(startCursor, 3, 1);
            positionsLayout.Controls.Add(startSeek, 4, 1);
            positionsLayout.Controls.Add(endLabel, 0, 2);
            positionsLayout.Controls.Add(end, 1, 2);
            positionsLayout.Controls.Add(endTime, 2, 2);
            positionsLayout.Controls.Add(endCursor, 3, 2);
            positionsLayout.Controls.Add(endSeek, 4, 2);
            positionsLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            positionsLayout.Location = new System.Drawing.Point(8, 101);
            positionsLayout.MinimumSize = new System.Drawing.Size(460, 0);
            positionsLayout.Name = "positionsLayout";
            positionsLayout.RowCount = 3;
            positionsLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
            positionsLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
            positionsLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
            positionsLayout.Size = new System.Drawing.Size(599, 100);
            positionsLayout.TabIndex = 1;
            // 
            // sampleHeader
            // 
            sampleHeader.AutoSize = true;
            sampleHeader.Location = new System.Drawing.Point(95, 0);
            sampleHeader.Name = "sampleHeader";
            sampleHeader.Size = new System.Drawing.Size(46, 15);
            sampleHeader.TabIndex = 0;
            sampleHeader.Text = "Sample";
            // 
            // timeHeader
            // 
            timeHeader.AutoSize = true;
            timeHeader.Location = new System.Drawing.Point(267, 0);
            timeHeader.Name = "timeHeader";
            timeHeader.Size = new System.Drawing.Size(51, 15);
            timeHeader.TabIndex = 1;
            timeHeader.Text = "Seconds";
            // 
            // startLabel
            // 
            startLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            startLabel.AutoSize = true;
            startLabel.Location = new System.Drawing.Point(3, 24);
            startLabel.Name = "startLabel";
            startLabel.Size = new System.Drawing.Size(60, 15);
            startLabel.TabIndex = 2;
            startLabel.Text = "Loop start";
            // 
            // start
            // 
            start.Dock = System.Windows.Forms.DockStyle.Fill;
            start.ForeColor = System.Drawing.SystemColors.ControlText;
            start.Location = new System.Drawing.Point(95, 18);
            start.Maximum = new decimal(new int[] { int.MaxValue, 0, 0, 0 });
            start.Name = "start";
            start.Size = new System.Drawing.Size(166, 23);
            start.TabIndex = 0;
            start.ThousandsSeparator = true;
            start.ValueChanged += LoopValueChanged;
            // 
            // startTime
            // 
            startTime.DecimalPlaces = 6;
            startTime.Dock = System.Windows.Forms.DockStyle.Fill;
            startTime.ForeColor = System.Drawing.SystemColors.ControlText;
            startTime.Increment = new decimal(new int[] { 1, 0, 0, 196608 });
            startTime.Location = new System.Drawing.Point(267, 18);
            startTime.Maximum = new decimal(new int[] { int.MaxValue, 0, 0, 0 });
            startTime.Name = "startTime";
            startTime.Size = new System.Drawing.Size(166, 23);
            startTime.TabIndex = 1;
            startTime.ValueChanged += TimeValueChanged;
            // 
            // startCursor
            // 
            startCursor.AutoSize = true;
            startCursor.BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
            startCursor.Dock = System.Windows.Forms.DockStyle.Fill;
            startCursor.FlatAppearance.BorderColor = System.Drawing.Color.DimGray;
            startCursor.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            startCursor.ForeColor = System.Drawing.Color.White;
            startCursor.Location = new System.Drawing.Point(439, 18);
            startCursor.Name = "startCursor";
            startCursor.Size = new System.Drawing.Size(84, 27);
            startCursor.TabIndex = 2;
            startCursor.Text = "Use cursor";
            startCursor.UseVisualStyleBackColor = false;
            startCursor.Click += StartCursor_Click;
            // 
            // startSeek
            // 
            startSeek.AutoSize = true;
            startSeek.BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
            startSeek.Dock = System.Windows.Forms.DockStyle.Fill;
            startSeek.FlatAppearance.BorderColor = System.Drawing.Color.DimGray;
            startSeek.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            startSeek.ForeColor = System.Drawing.Color.White;
            startSeek.Location = new System.Drawing.Point(529, 18);
            startSeek.Name = "startSeek";
            startSeek.Size = new System.Drawing.Size(67, 27);
            startSeek.TabIndex = 3;
            startSeek.Text = "Go to";
            startSeek.UseVisualStyleBackColor = false;
            startSeek.Click += StartSeek_Click;
            // 
            // endLabel
            // 
            endLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            endLabel.AutoSize = true;
            endLabel.Location = new System.Drawing.Point(3, 66);
            endLabel.Name = "endLabel";
            endLabel.Size = new System.Drawing.Size(85, 15);
            endLabel.TabIndex = 4;
            endLabel.Text = "End (exclusive)";
            // 
            // end
            // 
            end.Dock = System.Windows.Forms.DockStyle.Fill;
            end.ForeColor = System.Drawing.SystemColors.ControlText;
            end.Location = new System.Drawing.Point(95, 51);
            end.Maximum = new decimal(new int[] { int.MaxValue, 0, 0, 0 });
            end.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            end.Name = "end";
            end.Size = new System.Drawing.Size(166, 23);
            end.TabIndex = 4;
            end.ThousandsSeparator = true;
            end.Value = new decimal(new int[] { 1, 0, 0, 0 });
            end.ValueChanged += LoopValueChanged;
            // 
            // endTime
            // 
            endTime.DecimalPlaces = 6;
            endTime.Dock = System.Windows.Forms.DockStyle.Fill;
            endTime.ForeColor = System.Drawing.SystemColors.ControlText;
            endTime.Increment = new decimal(new int[] { 1, 0, 0, 196608 });
            endTime.Location = new System.Drawing.Point(267, 51);
            endTime.Maximum = new decimal(new int[] { int.MaxValue, 0, 0, 0 });
            endTime.Name = "endTime";
            endTime.Size = new System.Drawing.Size(166, 23);
            endTime.TabIndex = 5;
            endTime.ValueChanged += TimeValueChanged;
            // 
            // endCursor
            // 
            endCursor.AutoSize = true;
            endCursor.BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
            endCursor.Dock = System.Windows.Forms.DockStyle.Fill;
            endCursor.FlatAppearance.BorderColor = System.Drawing.Color.DimGray;
            endCursor.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            endCursor.ForeColor = System.Drawing.Color.White;
            endCursor.Location = new System.Drawing.Point(439, 51);
            endCursor.Name = "endCursor";
            endCursor.Size = new System.Drawing.Size(84, 46);
            endCursor.TabIndex = 6;
            endCursor.Text = "Use cursor";
            endCursor.UseVisualStyleBackColor = false;
            endCursor.Click += EndCursor_Click;
            // 
            // endSeek
            // 
            endSeek.AutoSize = true;
            endSeek.BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
            endSeek.Dock = System.Windows.Forms.DockStyle.Fill;
            endSeek.FlatAppearance.BorderColor = System.Drawing.Color.DimGray;
            endSeek.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            endSeek.ForeColor = System.Drawing.Color.White;
            endSeek.Location = new System.Drawing.Point(529, 51);
            endSeek.Name = "endSeek";
            endSeek.Size = new System.Drawing.Size(67, 46);
            endSeek.TabIndex = 7;
            endSeek.Text = "Go to";
            endSeek.UseVisualStyleBackColor = false;
            endSeek.Click += EndSeek_Click;
            // 
            // durationLabel
            // 
            durationLabel.AutoSize = true;
            durationLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            durationLabel.Location = new System.Drawing.Point(8, 214);
            durationLabel.Margin = new System.Windows.Forms.Padding(3, 10, 3, 6);
            durationLabel.Name = "durationLabel";
            durationLabel.Size = new System.Drawing.Size(599, 15);
            durationLabel.TabIndex = 2;
            durationLabel.Text = "Loop duration";
            // 
            // statusLabel
            // 
            statusLabel.AutoSize = true;
            statusLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            statusLabel.Location = new System.Drawing.Point(8, 238);
            statusLabel.Margin = new System.Windows.Forms.Padding(3, 3, 3, 10);
            statusLabel.Name = "statusLabel";
            statusLabel.Size = new System.Drawing.Size(599, 15);
            statusLabel.TabIndex = 3;
            statusLabel.Text = "Apply updates all channels. Save the resource to write changes to disk.";
            // 
            // actionsLayout
            // 
            actionsLayout.AutoSize = true;
            actionsLayout.Controls.Add(apply);
            actionsLayout.Controls.Add(reload);
            actionsLayout.Controls.Add(entire);
            actionsLayout.Controls.Add(close);
            actionsLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            actionsLayout.Location = new System.Drawing.Point(8, 266);
            actionsLayout.Name = "actionsLayout";
            actionsLayout.Size = new System.Drawing.Size(599, 33);
            actionsLayout.TabIndex = 2;
            // 
            // apply
            // 
            apply.AutoSize = true;
            apply.BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
            apply.FlatAppearance.BorderColor = System.Drawing.Color.DimGray;
            apply.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            apply.ForeColor = System.Drawing.Color.White;
            apply.Location = new System.Drawing.Point(3, 3);
            apply.Name = "apply";
            apply.Size = new System.Drawing.Size(75, 27);
            apply.TabIndex = 0;
            apply.Text = "Apply";
            apply.UseVisualStyleBackColor = false;
            apply.Click += Apply_Click;
            // 
            // reload
            // 
            reload.AutoSize = true;
            reload.BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
            reload.FlatAppearance.BorderColor = System.Drawing.Color.DimGray;
            reload.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            reload.ForeColor = System.Drawing.Color.White;
            reload.Location = new System.Drawing.Point(84, 3);
            reload.Name = "reload";
            reload.Size = new System.Drawing.Size(82, 27);
            reload.TabIndex = 1;
            reload.Text = "Reload loop";
            reload.UseVisualStyleBackColor = false;
            reload.Click += Reload_Click;
            // 
            // entire
            // 
            entire.AutoSize = true;
            entire.BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
            entire.FlatAppearance.BorderColor = System.Drawing.Color.DimGray;
            entire.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            entire.ForeColor = System.Drawing.Color.White;
            entire.Location = new System.Drawing.Point(172, 3);
            entire.Name = "entire";
            entire.Size = new System.Drawing.Size(133, 27);
            entire.TabIndex = 2;
            entire.Text = "Loop entire recording";
            entire.UseVisualStyleBackColor = false;
            entire.Click += Entire_Click;
            // 
            // close
            // 
            close.AutoSize = true;
            close.BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
            close.FlatAppearance.BorderColor = System.Drawing.Color.DimGray;
            close.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            close.ForeColor = System.Drawing.Color.White;
            close.Location = new System.Drawing.Point(311, 3);
            close.Name = "close";
            close.Size = new System.Drawing.Size(75, 27);
            close.TabIndex = 3;
            close.Text = "Close";
            close.UseVisualStyleBackColor = false;
            close.Click += Close_Click;
            // 
            // BwavLoopEditor
            // 
            AcceptButton = apply;
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            AutoScroll = true;
            BackColor = System.Drawing.Color.FromArgb(31, 31, 32);
            ClientSize = new System.Drawing.Size(615, 307);
            Controls.Add(mainLayout);
            DockAreas = WeifenLuo.WinFormsUI.Docking.DockAreas.Float | WeifenLuo.WinFormsUI.Docking.DockAreas.Document;
            ForeColor = System.Drawing.Color.White;
            MinimumSize = new System.Drawing.Size(400, 280);
            Name = "BwavLoopEditor";
            ShowInTaskbar = false;
            Text = "Edit audio loop";
            mainLayout.ResumeLayout(false);
            mainLayout.PerformLayout();
            positionsLayout.ResumeLayout(false);
            positionsLayout.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)start).EndInit();
            ((System.ComponentModel.ISupportInitialize)startTime).EndInit();
            ((System.ComponentModel.ISupportInitialize)end).EndInit();
            ((System.ComponentModel.ISupportInitialize)endTime).EndInit();
            actionsLayout.ResumeLayout(false);
            actionsLayout.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }
        #endregion
        private System.Windows.Forms.TableLayoutPanel mainLayout;
        private System.Windows.Forms.TableLayoutPanel positionsLayout;
        private System.Windows.Forms.FlowLayoutPanel actionsLayout;
        private System.Windows.Forms.Label audioInfoLabel;
        private System.Windows.Forms.Label mixedChannelsLabel;
        private System.Windows.Forms.CheckBox enabled;
        private System.Windows.Forms.Label sampleHeader;
        private System.Windows.Forms.Label timeHeader;
        private System.Windows.Forms.Label startLabel;
        private System.Windows.Forms.Label endLabel;
        private System.Windows.Forms.NumericUpDown start;
        private System.Windows.Forms.NumericUpDown end;
        private System.Windows.Forms.NumericUpDown startTime;
        private System.Windows.Forms.NumericUpDown endTime;
        private System.Windows.Forms.Button startCursor;
        private System.Windows.Forms.Button endCursor;
        private System.Windows.Forms.Button startSeek;
        private System.Windows.Forms.Button endSeek;
        private System.Windows.Forms.Label durationLabel;
        private System.Windows.Forms.Label statusLabel;
        private System.Windows.Forms.Button entire;
        private System.Windows.Forms.Button reload;
        private System.Windows.Forms.Button close;
        private System.Windows.Forms.Button apply;
    }
}
