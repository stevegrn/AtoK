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
            this.PcbNewLocation = new System.Windows.Forms.ListBox();
            this.TextEditorLocation = new System.Windows.Forms.ListBox();
            this.SuspendLayout();
            // 
            // PcbNew
            // 
            this.PcbNew.Location = new System.Drawing.Point(22, 24);
            this.PcbNew.Name = "PcbNew";
            this.PcbNew.Size = new System.Drawing.Size(110, 23);
            this.PcbNew.TabIndex = 0;
            this.PcbNew.Text = "PcbNew Location";
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
            // PcbNewLocation
            // 
            this.PcbNewLocation.FormattingEnabled = true;
            this.PcbNewLocation.Location = new System.Drawing.Point(182, 27);
            this.PcbNewLocation.Name = "PcbNewLocation";
            this.PcbNewLocation.Size = new System.Drawing.Size(295, 17);
            this.PcbNewLocation.TabIndex = 2;
            // 
            // TextEditorLocation
            // 
            this.TextEditorLocation.FormattingEnabled = true;
            this.TextEditorLocation.Location = new System.Drawing.Point(182, 75);
            this.TextEditorLocation.Name = "TextEditorLocation";
            this.TextEditorLocation.Size = new System.Drawing.Size(295, 17);
            this.TextEditorLocation.TabIndex = 3;
            // 
            // Options
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(498, 121);
            this.Controls.Add(this.TextEditorLocation);
            this.Controls.Add(this.PcbNewLocation);
            this.Controls.Add(this.TextEditor);
            this.Controls.Add(this.PcbNew);
            this.Name = "Options";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Options";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Options_FormClosing);
            this.Load += new System.EventHandler(this.Options_Load);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button PcbNew;
        private System.Windows.Forms.Button TextEditor;
        private System.Windows.Forms.ListBox PcbNewLocation;
        private System.Windows.Forms.ListBox TextEditorLocation;
    }
}