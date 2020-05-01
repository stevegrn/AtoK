namespace AtoK
{
    partial class Warnings
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
            this.YES = new System.Windows.Forms.Button();
            this.NO = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // YES
            // 
            this.YES.Location = new System.Drawing.Point(59, 85);
            this.YES.Name = "YES";
            this.YES.Size = new System.Drawing.Size(64, 21);
            this.YES.TabIndex = 0;
            this.YES.Text = "Yes";
            this.YES.UseVisualStyleBackColor = true;
            this.YES.Click += new System.EventHandler(this.YES_Click);
            // 
            // NO
            // 
            this.NO.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.NO.Location = new System.Drawing.Point(187, 85);
            this.NO.Name = "NO";
            this.NO.Size = new System.Drawing.Size(64, 21);
            this.NO.TabIndex = 1;
            this.NO.Text = "No";
            this.NO.UseVisualStyleBackColor = true;
            this.NO.Click += new System.EventHandler(this.NO_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(70, 24);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(171, 17);
            this.label1.TabIndex = 2;
            this.label1.Text = "Warnings were generated";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(67, 48);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(176, 17);
            this.label2.TabIndex = 3;
            this.label2.Text = "Do you want to view them?";
            // 
            // Warnings
            // 
            this.AcceptButton = this.YES;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.NO;
            this.CausesValidation = false;
            this.ClientSize = new System.Drawing.Size(310, 128);
            this.ControlBox = false;
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.NO);
            this.Controls.Add(this.YES);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "Warnings";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Warnings";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button YES;
        private System.Windows.Forms.Button NO;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
    }
}