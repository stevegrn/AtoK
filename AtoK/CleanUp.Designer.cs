namespace AtoK
{
    partial class CleanUp
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
            this.CurrentFile = new System.Windows.Forms.Button();
            this.AllFiles = new System.Windows.Forms.Button();
            this.Cancel = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // CurrentFile
            // 
            this.CurrentFile.Location = new System.Drawing.Point(29, 33);
            this.CurrentFile.Name = "CurrentFile";
            this.CurrentFile.Size = new System.Drawing.Size(75, 23);
            this.CurrentFile.TabIndex = 0;
            this.CurrentFile.Text = "Current File";
            this.CurrentFile.UseVisualStyleBackColor = true;
            this.CurrentFile.Click += new System.EventHandler(this.CurrentFile_Click);
            // 
            // AllFiles
            // 
            this.AllFiles.Location = new System.Drawing.Point(164, 33);
            this.AllFiles.Name = "AllFiles";
            this.AllFiles.Size = new System.Drawing.Size(75, 23);
            this.AllFiles.TabIndex = 1;
            this.AllFiles.Text = "All Files";
            this.AllFiles.UseVisualStyleBackColor = true;
            this.AllFiles.Click += new System.EventHandler(this.AllFiles_Click);
            // 
            // Cancel
            // 
            this.Cancel.Location = new System.Drawing.Point(293, 33);
            this.Cancel.Name = "Cancel";
            this.Cancel.Size = new System.Drawing.Size(75, 23);
            this.Cancel.TabIndex = 2;
            this.Cancel.Text = "Cancel";
            this.Cancel.UseVisualStyleBackColor = true;
            this.Cancel.Click += new System.EventHandler(this.Cancel_Click);
            // 
            // CleanUp
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(389, 88);
            this.ControlBox = false;
            this.Controls.Add(this.Cancel);
            this.Controls.Add(this.AllFiles);
            this.Controls.Add(this.CurrentFile);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "CleanUp";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "CleanUp";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.CleanUp_FormClosing);
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.CleanUp_FormClosed);
            this.Load += new System.EventHandler(this.CleanUp_Load);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button CurrentFile;
        private System.Windows.Forms.Button AllFiles;
        private System.Windows.Forms.Button Cancel;
    }
}