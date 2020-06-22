using System;
using System.Windows.Forms;
using System.IO;
using ConvertToKicad;

namespace AtoK
{
    public partial class Options : Form
    {
        public bool Pcbnew515;

        public Options()
        {
            InitializeComponent();

            PcbnewLocation.Text     = Properties.Settings.Default.PcbnewLocation;
            TextEditorLocation.Text = Properties.Settings.Default.TextEditorLocation;
            PcbnewVersion.Checked   = Properties.Settings.Default.PcbnewVersion;
            Globals.PcbnewVersion   = PcbnewVersion.Checked;
        }

        private void PcbNew_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                InitialDirectory = Form1.PcbnewLocation != "" ? Path.GetDirectoryName(Form1.PcbnewLocation) : "C:\\",
                Filter = "PcbNew (pcbnew.exe)|pcbnew.exe|All files (*.*)|*.*",
                FilterIndex = 1,
                FileName = Path.GetFileName(Form1.PcbnewLocation),
                RestoreDirectory = true
            };

            string test = PcbnewLocation.Text;
            DialogResult result = openFileDialog.ShowDialog(); // Show the dialog.
            if (result == DialogResult.OK) // Test result.
            {
                PcbnewLocation.Items.Clear();
                PcbnewLocation.Items.Add(openFileDialog.FileName);
                PcbnewLocation.Hide();
                PcbnewLocation.Show();
                PcbnewLocation.Text = openFileDialog.FileName;
                PcbnewLocation.TopIndex = 0;
                PcbnewLocation.Update();
                Application.DoEvents();
            }
        }

        private void TextEditor_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                InitialDirectory = Form1.TextEditorLoc != "" ? Path.GetDirectoryName(Form1.TextEditorLoc) : "C:\\",
                Filter = "All files (*.exe)|*.*",
                FilterIndex = 1,
                RestoreDirectory = true,
                FileName = Path.GetFileName(Form1.TextEditorLoc)
            };
            DialogResult result = openFileDialog.ShowDialog(); // Show the dialog.
            if (result == DialogResult.OK) // Test result.
            {
                TextEditorLocation.Items.Clear();
                TextEditorLocation.Items.Add(openFileDialog.FileName);
                TextEditorLocation.Hide();
                TextEditorLocation.Show();
                TextEditorLocation.Text = openFileDialog.FileName;
                TextEditorLocation.Update();
                Application.DoEvents();
            }
        }

        private void Options_FormClosing(object sender, FormClosingEventArgs e)
        {
        }

        private void Options_Load(object sender, EventArgs e)
        {
            PcbnewLocation.Items.Clear();
            PcbnewLocation.Items.Add(Form1.PcbnewLocation);
            TextEditorLocation.Items.Clear();
            TextEditorLocation.Items.Add(Form1.TextEditorLoc);
            PcbnewVersion.Checked = Properties.Settings.Default.PcbnewVersion;
            ShowWarnings.Checked  = Properties.Settings.Default.ShowWarningsDialog;
            ReportFile.Checked = Properties.Settings.Default.ReportFile;
            Globals.PcbnewVersion = PcbnewVersion.Checked;
        }

        private void CANCEL_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void OK_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.PcbnewLocation     = PcbnewLocation.Text;
            Properties.Settings.Default.TextEditorLocation = TextEditorLocation.Text;
            Properties.Settings.Default.Save();
            Properties.Settings.Default.PcbnewVersion = PcbnewVersion.Checked;
            Properties.Settings.Default.ShowWarningsDialog = ShowWarnings.Checked;
            Globals.PcbnewVersion = PcbnewVersion.Checked;
            Properties.Settings.Default.ReportFile = ReportFile.Checked;
            Properties.Settings.Default.Save();

            Form1.PcbnewLocation = PcbnewLocation.Text;
            Form1.TextEditorLoc  = TextEditorLocation.Text;
            this.DialogResult    = DialogResult.OK;
            this.Close();
        }

        private void TextEditorLocation_KeyPress(object sender, KeyPressEventArgs e)
        {

        }

        private void TextEditorLocation_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                e.Handled = false;
                TextEditorLocation.Items.Clear();
                TextEditorLocation.Items.Add("");
                TextEditorLocation.Text = "";
                Update();
            }
        }

        private void PcbnewLocation_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                e.Handled = false;
                PcbnewLocation.Items.Clear();
                PcbnewLocation.Items.Add("");
                PcbnewLocation.Text = "";
                Update();
            }
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
        }

        private void PcbnewVersion_Click(object sender, EventArgs e)
        {
        }
    }
}
