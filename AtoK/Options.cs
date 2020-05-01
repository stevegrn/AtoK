using System;
using System.Windows.Forms;
using System.IO;

namespace AtoK
{
    public partial class Options : Form
    {
        public Options()
        {
            InitializeComponent();

            PcbnewLocation.Text     = Properties.Settings.Default.PcbnewLocation;
            TextEditorLocation.Text = Properties.Settings.Default.TextEditorLocation;
        }

        private void PcbNew_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = Form1.PcbnewLocation!=""?Path.GetDirectoryName(Form1.PcbnewLocation):"C:\\";
            openFileDialog.Filter = "PcbNew (pcbnew.exe)|pcbnew.exe|All files (*.*)|*.*";
            openFileDialog.FilterIndex = 1;
            openFileDialog.FileName = Path.GetFileName(Form1.PcbnewLocation);
            openFileDialog.RestoreDirectory = true;

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
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = Form1.TextEditorLoc != "" ? Path.GetDirectoryName(Form1.TextEditorLoc) : "C:\\";
            openFileDialog.Filter = "All files (*.exe)|*.*";
            openFileDialog.FilterIndex = 1;
            openFileDialog.RestoreDirectory = true;
            openFileDialog.FileName = Path.GetFileName(Form1.TextEditorLoc);
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

            Form1.PcbnewLocation = PcbnewLocation.Text;
            Form1.TextEditorLoc  = TextEditorLocation.Text;
            this.DialogResult = DialogResult.OK;
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
    }
}
