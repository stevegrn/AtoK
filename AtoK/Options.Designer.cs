namespace AtoK
{
    partial class Options
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.PcbNew = new System.Windows.Forms.Button();
            this.TextEditor = new System.Windows.Forms.Button();
            this.PcbnewLocation = new System.Windows.Forms.ListBox();
            this.TextEditorLocation = new System.Windows.Forms.ListBox();
            this.OK = new System.Windows.Forms.Button();
            this.CANCEL = new System.Windows.Forms.Button();
            this.PcbnewVersion = new System.Windows.Forms.CheckBox();
            this.ShowWarnings = new System.Windows.Forms.CheckBox();
            this.ReportFile = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // PcbNew
            // 
            this.PcbNew.Location = new System.Drawing.Point(22, 24);
            this.PcbNew.Name = "PcbNew";
            this.PcbNew.Size = new System.Drawing.Size(110, 23);
            this.PcbNew.TabIndex = 0;
            this.PcbNew.Text = "Pcbnew Location";
            this.PcbNew.UseVisualStyleBackColor = true;
            this.PcbNew.Click += new System.EventHandler(this.PcbNew_Click);
            // 
            // TextEditor
            // 
            this.TextEditor.Location = new System.Drawing.Point(22, 70);
            this.TextEditor.Name = "TextEditor";
            this.TextEditor.Size = new System.Drawing.Size(110, 26);
            this.TextEditor.TabIndex = 1;
            this.TextEditor.Text = "Text Editor Button";
            this.TextEditor.UseVisualStyleBackColor = true;
            this.TextEditor.Click += new System.EventHandler(this.TextEditor_Click);
            // 
            // PcbnewLocation
            // 
            this.PcbnewLocation.FormattingEnabled = true;
            this.PcbnewLocation.Location = new System.Drawing.Point(182, 27);
            this.PcbnewLocation.Name = "PcbnewLocation";
            this.PcbnewLocation.Size = new System.Drawing.Size(295, 17);
            this.PcbnewLocation.TabIndex = 2;
            this.PcbnewLocation.KeyDown += new System.Windows.Forms.KeyEventHandler(this.PcbnewLocation_KeyDown);
            // 
            // TextEditorLocation
            // 
            this.TextEditorLocation.FormattingEnabled = true;
            this.TextEditorLocation.Location = new System.Drawing.Point(182, 75);
            this.TextEditorLocation.Name = "TextEditorLocation";
            this.TextEditorLocation.Size = new System.Drawing.Size(295, 17);
            this.TextEditorLocation.TabIndex = 3;
            this.TextEditorLocation.KeyDown += new System.Windows.Forms.KeyEventHandler(this.TextEditorLocation_KeyDown);
            this.TextEditorLocation.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.TextEditorLocation_KeyPress);
            // 
            // OK
            // 
            this.OK.Location = new System.Drawing.Point(110, 218);
            this.OK.Name = "OK";
            this.OK.Size = new System.Drawing.Size(110, 26);
            this.OK.TabIndex = 4;
            this.OK.Text = "OK";
            this.OK.UseVisualStyleBackColor = true;
            this.OK.Click += new System.EventHandler(this.OK_Click);
            // 
            // CANCEL
            // 
            this.CANCEL.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.CANCEL.Location = new System.Drawing.Point(288, 218);
            this.CANCEL.Name = "CANCEL";
            this.CANCEL.Size = new System.Drawing.Size(110, 26);
            this.CANCEL.TabIndex = 5;
            this.CANCEL.Text = "CANCEL";
            this.CANCEL.UseVisualStyleBackColor = true;
            this.CANCEL.Click += new System.EventHandler(this.CANCEL_Click);
            // 
            // PcbnewVersion
            // 
            this.PcbnewVersion.AutoSize = true;
            this.PcbnewVersion.Location = new System.Drawing.Point(176, 148);
            this.PcbnewVersion.Name = "PcbnewVersion";
            this.PcbnewVersion.Size = new System.Drawing.Size(145, 17);
            this.PcbnewVersion.TabIndex = 6;
            this.PcbnewVersion.Text = "Pcbnew Version <= 5.1.5";
            this.PcbnewVersion.UseVisualStyleBackColor = true;
            this.PcbnewVersion.CheckedChanged += new System.EventHandler(this.checkBox1_CheckedChanged);
            this.PcbnewVersion.Click += new System.EventHandler(this.PcbnewVersion_Click);
            // 
            // ShowWarnings
            // 
            this.ShowWarnings.AutoSize = true;
            this.ShowWarnings.Location = new System.Drawing.Point(176, 171);
            this.ShowWarnings.Name = "ShowWarnings";
            this.ShowWarnings.Size = new System.Drawing.Size(146, 17);
            this.ShowWarnings.TabIndex = 7;
            this.ShowWarnings.Text = "Show Warnings Dialogue";
            this.ShowWarnings.UseVisualStyleBackColor = true;
            // 
            // ReportFile
            // 
            this.ReportFile.AutoSize = true;
            this.ReportFile.Location = new System.Drawing.Point(176, 125);
            this.ReportFile.Name = "ReportFile";
            this.ReportFile.Size = new System.Drawing.Size(146, 17);
            this.ReportFile.TabIndex = 8;
            this.ReportFile.Text = "Output Errors to report file";
            this.ReportFile.UseVisualStyleBackColor = true;
            // 
            // Options
            // 
            this.AcceptButton = this.OK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.CANCEL;
            this.ClientSize = new System.Drawing.Size(498, 280);
            this.Controls.Add(this.ReportFile);
            this.Controls.Add(this.ShowWarnings);
            this.Controls.Add(this.PcbnewVersion);
            this.Controls.Add(this.CANCEL);
            this.Controls.Add(this.OK);
            this.Controls.Add(this.TextEditorLocation);
            this.Controls.Add(this.PcbnewLocation);
            this.Controls.Add(this.TextEditor);
            this.Controls.Add(this.PcbNew);
            this.Name = "Options";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Options";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Options_FormClosing);
            this.Load += new System.EventHandler(this.Options_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button PcbNew;
        private System.Windows.Forms.Button TextEditor;
        private System.Windows.Forms.ListBox PcbnewLocation;
        private System.Windows.Forms.ListBox TextEditorLocation;
        private System.Windows.Forms.Button OK;
        private System.Windows.Forms.Button CANCEL;
        private System.Windows.Forms.CheckBox PcbnewVersion;
        private System.Windows.Forms.CheckBox ShowWarnings;
        private System.Windows.Forms.CheckBox ReportFile;
    }
}