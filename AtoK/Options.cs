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
            PcbNewLocation.Text = Form1.PcbNewLocation;
            TextEditorLocation.Text = Form1.TextEditorLocation;
        }

        private void PcbNew_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = Form1.PcbNewLocation!=""?Path.GetDirectoryName(Form1.PcbNewLocation):"C:\\";
            openFileDialog.Filter = "PcbNew (pcbnew.exe)|pcbnew.exe|All files (*.*)|*.*";
            openFileDialog.FilterIndex = 1;
            openFileDialog.FileName = Path.GetFileName(Form1.PcbNewLocation);
            openFileDialog.RestoreDirectory = true;

            string test = PcbNewLocation.Text;
            DialogResult result = openFileDialog.ShowDialog(); // Show the dialog.
            if (result == DialogResult.OK) // Test result.
            {
                PcbNewLocation.Items.Clear();
                PcbNewLocation.Items.Add(openFileDialog.FileName);
                PcbNewLocation.Hide();
                PcbNewLocation.Show();
                Form1.PcbNewLocation = openFileDialog.FileName;
                PcbNewLocation.TopIndex = 0;
                PcbNewLocation.Update();
                Application.DoEvents();
            }
        }

        private void TextEditor_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = Form1.TextEditorLocation != "" ? Path.GetDirectoryName(Form1.TextEditorLocation) : "C:\\";
            openFileDialog.Filter = "All files (*.exe)|*.*";
            openFileDialog.FilterIndex = 1;
            openFileDialog.RestoreDirectory = true;
            openFileDialog.FileName = Path.GetFileName(Form1.TextEditorLocation);
            DialogResult result = openFileDialog.ShowDialog(); // Show the dialog.
            if (result == DialogResult.OK) // Test result.
            {
                TextEditorLocation.Items.Clear();
                TextEditorLocation.Items.Add(openFileDialog.FileName);
                TextEditorLocation.Hide();
                TextEditorLocation.Show();
                Form1.TextEditorLocation = openFileDialog.FileName;
                PcbNewLocation.TopIndex = 0;
                TextEditorLocation.Update();
                Application.DoEvents();
            }
        }

        private void Options_FormClosing(object sender, FormClosingEventArgs e)
        {
            Form1.PcbNewLocation = PcbNewLocation.Text;
            Form1.TextEditorLocation = TextEditorLocation.Text;
        }

        private void Options_Load(object sender, EventArgs e)
        {
            PcbNewLocation.Items.Clear();
            PcbNewLocation.Items.Add(Form1.PcbNewLocation);
            TextEditorLocation.Items.Clear();
            TextEditorLocation.Items.Add(Form1.TextEditorLocation);

        }
    }
}
