using System;
using System.Collections;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ConvertToKicad;
using System.Diagnostics;
using System.Text;

namespace AtoK
{

    public partial class Form1 : Form
    {
        ConcurrentQueue<string> dq = new ConcurrentQueue<string>();
        //       Stream myStream;
        //        StreamWriter myWriter;
        public static Thread t; // ues for running the convert in  the background
        private delegate void UpdateOutputDelegate(string s, Color colour);

        private UpdateOutputDelegate updateoutputDelegate = null;
        private Color BackColor;

        public void UpdateOutput(string s, Color colour)
        {
            outputList_Add(s, colour);
            outputList.Update();
        }


        public class Line
        {
            public string Str;
            public Color ForeColor;

            public Line(string str, Color color)
            {
                Str = str;
                ForeColor = color;
            }
        };

        int outputlist_width = 0;
        ArrayList lines = new ArrayList();
        string ComboboxItems = "";

        public Form1()
        {
            Screen scrn = Screen.FromControl(this);

            InitializeComponent();

            outputList_Initialize();
            FileHistory_Initialize();
            SaveExtractedDocs.CheckState = Properties.Settings.Default.SaveDocs ? CheckState.Checked : CheckState.Unchecked;
            LibraryGen.CheckState = Properties.Settings.Default.GenLib ? CheckState.Checked : CheckState.Unchecked;
            Verbose.CheckState = Properties.Settings.Default.Verbose ? CheckState.Checked : CheckState.Unchecked;
            FileHistory.Text = Properties.Settings.Default.LastFile;
            FileHistory.Select(FileHistory.Text.Length, 0); // scroll to make filename visible
            FileHistory.Text = FileHistory.Text;

            ComboboxItems = Properties.Settings.Default.ComboboxItems;
            string[] Items = ComboboxItems.Split(';');
            foreach (var item in Items)
            {
                if (item != "")
                    FileHistory.Items.Insert(0, item);
            }
            FileHistory.SelectedIndex = Properties.Settings.Default.ComboBoxIndex;
        }

        // shutdown the worker thread when the form closes
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
        }

        #region Output window

        bool scrolling = true;

        ContextMenu popUpMenu;
        ContextMenu ComboBoxMenu;

        private void FileHistory_Delete(object sender, EventArgs e)
        {
            // delete the selected item

        }

        private void FileHistory_Initialize()
        {
            ComboBoxMenu = new ContextMenu();
            ComboBoxMenu.MenuItems.Add("&Delete", new EventHandler(FileHistory_Delete));
            popUpMenu.MenuItems[0].Visible = true;
            popUpMenu.MenuItems[0].Enabled = true;
            popUpMenu.MenuItems[0].Shortcut = Shortcut.CtrlX;
            popUpMenu.MenuItems[0].ShowShortcut = true;
        }

        public Line outputList_Add(string str, Color color)
        {
            Invoke((MethodInvoker)(() => outputList.BeginUpdate()));
            Line newLine = new Line(str, color);
            lines.Add(newLine);
            int testWidth = TextRenderer.MeasureText(str,
                                            outputList.Font, outputList.ClientSize,
                                            TextFormatFlags.NoPrefix).Width;
            if (testWidth > outputlist_width)
                outputlist_width = testWidth;

            Invoke((MethodInvoker)(() => outputList.HorizontalExtent = outputlist_width));
            Invoke((MethodInvoker)(() => outputList.Items.Add(newLine)));
            Invoke((MethodInvoker)(() => outputList_Scroll()));
            Invoke((MethodInvoker)(() => outputList.EndUpdate()));
            return newLine;
        }

        public void outputList_Update()
        {
            Invoke((MethodInvoker)(() => outputList.Update()));
        }

        private void outputList_Initialize()
        {
            // owner draw for listbox so we can add color
            outputList.DrawMode = DrawMode.OwnerDrawFixed;
            outputList.DrawItem += new DrawItemEventHandler(outputList_DrawItem);
            outputList.ClearSelected();

            // build the outputList context menu
            popUpMenu = new ContextMenu();
            popUpMenu.MenuItems.Add("&Copy", new EventHandler(outputList_Copy));
            popUpMenu.MenuItems[0].Visible = true;
            popUpMenu.MenuItems[0].Enabled = false;
            popUpMenu.MenuItems[0].Shortcut = Shortcut.CtrlC;
            popUpMenu.MenuItems[0].ShowShortcut = true;

            popUpMenu.MenuItems.Add("Copy All", new EventHandler(outputList_CopyAll));
            popUpMenu.MenuItems[1].Visible = true;

            popUpMenu.MenuItems.Add("Select &All", new EventHandler(outputList_SelectAll));
            popUpMenu.MenuItems[2].Visible = true;
            popUpMenu.MenuItems[2].Shortcut = Shortcut.CtrlA;
            popUpMenu.MenuItems[2].ShowShortcut = true;

            popUpMenu.MenuItems.Add("Unselect", new EventHandler(outputList_ClearSelected));
            popUpMenu.MenuItems[3].Visible = true;
            popUpMenu.MenuItems.Add("Delete selected", new EventHandler(outputList_DeleteSelected));
            popUpMenu.MenuItems[4].Visible = true;
            popUpMenu.MenuItems.Add("Clear All", new EventHandler(outputList_ClearAll));
            popUpMenu.MenuItems[5].Visible = true;
            popUpMenu.MenuItems.Add("Toggle Scrolling", new EventHandler(outputList_ToggleScrolling));

            outputList.ContextMenu = popUpMenu;
        }

        void outputList_DrawItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index >= 0 && e.Index < outputList.Items.Count)
            {
                Line line = (Line)outputList.Items[e.Index];

                // if selected, make the text color readable
                Color color = line.ForeColor;
                if ((e.State & DrawItemState.Selected) == DrawItemState.Selected)
                {
                    color = Color.White;    // make it readable
                }

                e.Graphics.DrawString(line.Str, e.Font, new SolidBrush(color),
                    e.Bounds, StringFormat.GenericDefault);
            }
            e.DrawFocusRectangle();
        }

        void outputList_Scroll()
        {
            if (scrolling)
            {
                int itemsPerPage = (int)(outputList.Height / outputList.ItemHeight);
                outputList.TopIndex = outputList.Items.Count - itemsPerPage;
                if (outputList.HorizontalScrollbar && (itemsPerPage < outputList.Items.Count))
                    outputList.TopIndex++;
            }
        }

        private void outputList_SelectedIndexChanged(object sender, EventArgs e)
        {
            popUpMenu.MenuItems[0].Enabled = (outputList.SelectedItems.Count > 0);
        }

        private void outputList_Copy(object sender, EventArgs e)
        {
            int iCount = outputList.SelectedItems.Count;
            if (iCount > 0)
            {
                String[] source = new String[iCount];
                for (int i = 0; i < iCount; ++i)
                {
                    source[i] = ((Line)outputList.SelectedItems[i]).Str;
                }

                String dest = String.Join("\r\n", source);
                Clipboard.SetText(dest);
            }
        }

        private void outputList_CopyAll(object sender, EventArgs e)
        {
            int iCount = outputList.Items.Count;
            if (iCount > 0)
            {
                String[] source = new String[iCount];
                for (int i = 0; i < iCount; ++i)
                {
                    source[i] = ((Line)outputList.Items[i]).Str;
                }

                String dest = String.Join("\r\n", source);
                Clipboard.SetText(dest);
            }
        }

        private void outputList_SelectAll(object sender, EventArgs e)
        {
            outputList.BeginUpdate();
            for (int i = 0; i < outputList.Items.Count; ++i)
            {
                outputList.SetSelected(i, true);
            }
            outputList.EndUpdate();
        }

        private void outputList_ClearSelected(object sender, EventArgs e)
        {
            outputList.ClearSelected();
            outputList.SelectedItem = -1;
        }

        private void outputList_DeleteSelected(object sender, EventArgs e)
        {
            outputList.BeginUpdate();
            // Remove each item in reverse order to maintain integrity
            var selectedIndices = new List<int>(outputList.SelectedIndices.Cast<int>());
            selectedIndices.Reverse();
            selectedIndices.ForEach(index => outputList.Items.RemoveAt(index));

            outputList.SelectedItem = -1;
            outputList.EndUpdate();
        }

        private void outputList_ClearAll(object sender, EventArgs e)
        {
            outputList.Items.Clear();
            outputList.SelectedItem = -1;
        }

        #endregion

        #region User interaction

        /// <summary>
		/// toggle scrolling
		/// </summary>
		private void outputList_ToggleScrolling(object sender, EventArgs e)
        {
            scrolling = !scrolling;
            outputList_Scroll();
        }

        #endregion

        bool isPointVisibleOnAScreen(Point p)
        {
            foreach (Screen s in Screen.AllScreens)
            {
                if (p.X < s.Bounds.Right && p.X > s.Bounds.Left && p.Y > s.Bounds.Top && p.Y < s.Bounds.Bottom)
                    return true;
            }
            return false;
        }

        bool isFormFullyVisible(Point p, Size size)
        {
            return isPointVisibleOnAScreen(p)
                && isPointVisibleOnAScreen(new Point(p.X + size.Width, p.Y))
                && isPointVisibleOnAScreen(new Point(p.X + size.Width, p.Y + size.Height));
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            updateoutputDelegate = new UpdateOutputDelegate(UpdateOutput);

            if (!isFormFullyVisible(Properties.Settings.Default.F1Location, Properties.Settings.Default.F1Size) || Properties.Settings.Default.F1Size.Width == 0 || Properties.Settings.Default.F1Size.Height == 0)
            {
                // first start or form not visible due to monitor setup changing since last saved
                // optional: add default values
            }
            else
            {
                this.WindowState = Properties.Settings.Default.F1State;

                // we don't want a minimized window at startup
                if (this.WindowState == FormWindowState.Minimized) this.WindowState = FormWindowState.Normal;

                this.Location = Properties.Settings.Default.F1Location;
                this.Size = Properties.Settings.Default.F1Size;
            }
            SaveExtractedDocs.CheckState = Properties.Settings.Default.SaveDocs ? CheckState.Checked : CheckState.Unchecked;
            LibraryGen.CheckState = Properties.Settings.Default.GenLib ? CheckState.Checked : CheckState.Unchecked;
            Verbose.CheckState = Properties.Settings.Default.Verbose ? CheckState.Checked : CheckState.Unchecked;
            ConvertPCBDoc.Verbose = Verbose.CheckState == CheckState.Checked;
            string Combo = Properties.Settings.Default.ComboboxItems;

            string[] items = Combo.Split(';');

            FileHistory.Items.Clear();
            FileHistory.Items.AddRange(items);
            FileHistory.SelectedIndex = Properties.Settings.Default.ComboBoxIndex;
            BackColor = button1.BackColor;
        }

        private void Form1_Closing(object sender, FormClosingEventArgs e)
        {
            if (t != null && t.IsAlive)
            {
                t.Abort();
            }
            Properties.Settings.Default.F1State = this.WindowState;
            if (this.WindowState == FormWindowState.Normal)
            {
                // save location and size if the state is normal
                Properties.Settings.Default.F1Location = this.Location;
                Properties.Settings.Default.F1Size = this.Size;
            }
            else
            {
                // save the RestoreBounds if the form is minimized or maximized!
                Properties.Settings.Default.F1Location = this.RestoreBounds.Location;
                Properties.Settings.Default.F1Size = this.RestoreBounds.Size;
            }
            Properties.Settings.Default.SaveDocs = SaveExtractedDocs.CheckState == CheckState.Checked;
            Properties.Settings.Default.GenLib = LibraryGen.CheckState == CheckState.Checked;
            Properties.Settings.Default.Verbose = Verbose.CheckState == CheckState.Checked;
            Properties.Settings.Default.LastFile = FileHistory.Text;

            StringBuilder sb = new StringBuilder();
            foreach (var item in FileHistory.Items)
            {
                if (item.ToString() != "")
                    sb.Append(item.ToString() + ";");
            }
            string Items = sb.ToString();
            char[] charsToTrim = { ';' };
            string It = Items.Substring(0, Items.Length - 1);

            Properties.Settings.Default.ComboboxItems = It;
            Properties.Settings.Default.ComboBoxIndex = FileHistory.SelectedIndex;

            // don't forget to save the settings
            Properties.Settings.Default.Save();
        }

        public bool ControlInvokeRequired(Control c, Action a)
        {
            if (c.InvokeRequired) c.Invoke(new MethodInvoker(delegate { a(); }));
            else return false;

            return true;
        }

        private string GetFilename()
        {
            if (ControlInvokeRequired(FileHistory, () => GetFilename())) return "";
            return FileHistory.Text;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (button1.Text == "Cancel")
            {
                t.Abort(); // terminate the thread
                while (ConvertPCBDoc.ConvertRunning)
                    Thread.Sleep(1000);
                t = null;
                button1.Text = "Convert";
                busy.Select();
                EnableControls();
                return;
            }
            if (File.Exists(FileHistory.Text))
            {
                button1.Text = "Cancel";
                this.Update();
                LibraryGen.Enabled = false;
                SaveExtractedDocs.Enabled = false;
                Verbose.Enabled = false;
                FileHistory.Enabled = false;
                //                button1.Enabled = false;
                button2.Enabled = false;
                CleanUp.Enabled = false;
                LaunchPCBNew.Enabled = false;
                Edit.Enabled = false;
                ClearHistory.Enabled = false;
                //start the conversion
                Cursor.Current = Cursors.WaitCursor;
                t = new Thread((object Filename) =>
                {
                    Program.ConvertPCB.ConvertFile(Filename.ToString(), SaveExtractedDocs.CheckState == CheckState.Checked, LibraryGen.CheckState == CheckState.Checked);
                });

                t.Start(FileHistory.Text);
                timer1.Enabled = true;
                timer1.Interval = 500;
            }
            else
                outputList_Add("File \"{FileHistory.Text}\"doesn't exist", System.Drawing.Color.Red);
        }

        public void EnableControls()
        {
            LibraryGen.Enabled = true;
            LibraryGen.Update();
            SaveExtractedDocs.Enabled = true;
            SaveExtractedDocs.Update();
            Verbose.Enabled = true;
            Verbose.Update();
            FileHistory.Enabled = true;
            FileHistory.Update();
            button1.Enabled = true;
            button1.Update();
            button2.Enabled = true;
            button2.Update();
            CleanUp.Enabled = true;
            CleanUp.Update();
            LaunchPCBNew.Enabled = true;
            LaunchPCBNew.Update();
            ClearHistory.Enabled = true;
            ClearHistory.Update();
            Edit.Enabled = false;
            Edit.Update();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            busy.Select();
            OpenFileDialog openFileDialog1 = new OpenFileDialog
            {

                InitialDirectory = @"D:\",
                Title = "Browse Altium PCBDoc Files",
                CheckFileExists = true,
                CheckPathExists = true,

                DefaultExt = "pcbdoc",
                Filter = "pcb files (*.pcbdoc)|*.pcbdoc|Circuitmaker files (*.cmpcbdoc):|*.cmpcbdoc",
                FilterIndex = 2,
                RestoreDirectory = true,

                ReadOnlyChecked = true,
                ShowReadOnly = true
            };
            openFileDialog1.Multiselect = true;
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                FileHistory.Text = openFileDialog1.FileName;
                FileHistory.Items.AddRange(openFileDialog1.FileNames);
                // only add to history if not already there
                bool found = false;
                foreach (var l in FileHistory.Items)
                {
                    if ((String)l == FileHistory.Text)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    FileHistory.Items.Insert(0, FileHistory.Text);
                    FileHistory.SelectedIndex = 0;
                    FileHistory.Select(FileHistory.Text.Length, 0); // scroll to make filename visible
                }
                FileHistory.SelectedItem = 0;
                FileHistory.Select(FileHistory.Text.Length, 0); // scroll to make filename visible
            }

        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            this.MinimumSize = new Size(this.Width, 0);
            this.MaximumSize = new Size(this.Width, Int32.MaxValue);
            Control control = (Control)sender;
            // resize the output window 
            // set top to bottom of button1
            // set bottom to bottom of form
            // set left to left of form
            // set right to right of form
            control.Width = button1.Right;
            outputList.Width = this.Width - 30;
            outputList.Left = FileHistory.Left;
            outputList.Top = Verbose.Bottom + 10;
            outputList.Height = control.Height - Verbose.Bottom - 50;
        }

        private void Verbose_Click(object sender, EventArgs e)
        {
            ConvertPCBDoc.Verbose = Verbose.Checked;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (button1.BackColor == Color.Green)
                button1.BackColor = BackColor;
            else
                button1.BackColor = Color.Green;
            if (t == null || t.IsAlive == false)
            {
                button1.Text = "Convert";
                EnableControls();
                timer1.Enabled = false;
                Cursor.Current = Cursors.Default;
                busy.Enabled = false;
                busy.Visible = false;
                busy.Hide();
                Edit.Enabled = true;
                ClearHistory.Enabled = true;
                button1.BackColor = BackColor;
                this.Update();
            }
            else
            {
                busy.Enabled = true;
                busy.Visible = false;
            }
        }

        private void LaunchPCBNew_Click(object sender, EventArgs e)
        {
            busy.Select();
            Process p = new Process();
            if (FileHistory.Text != "" && File.Exists(FileHistory.Text))
            {
                string UnpackDirectory = FileHistory.Text.Substring(0, FileHistory.Text.LastIndexOf('.')) + "-Kicad";
                if (Directory.Exists(UnpackDirectory))
                {
                    int index = FileHistory.Text.LastIndexOf('.');
                    string FileName;
                    FileName = FileHistory.Text.Substring(FileHistory.Text.LastIndexOf('\\') + 1);
                    FileName = FileName.Substring(0, FileName.LastIndexOf('.'));

                    string output_filename = UnpackDirectory + "\\" + FileName + ".kicad_pcb";

                    if (File.Exists(output_filename))
                    {
                        try
                        {
                            p.StartInfo = new ProcessStartInfo("C:\\Program Files\\KiCad\\bin\\pcbnew.exe", "\"" + output_filename + "\"");
                            p.StartInfo.RedirectStandardOutput = false;
                            p.StartInfo.RedirectStandardError = true;
                            p.StartInfo.UseShellExecute = false;
                            p.Start();
                        }
                        catch (System.ComponentModel.Win32Exception)
                        {
                            ConvertPCBDoc.OutputError("Couldn't launch PcbNew");
                        }
                    }
                    else
                        ConvertPCBDoc.OutputError($"Launch failed as file \"{output_filename}\" doesn't exist");

                }
                else
                {
                    ConvertPCBDoc.OutputError($"Directory \"{UnpackDirectory}\" doesn't exist");
                }
            }
            else
                ConvertPCBDoc.OutputError($"Launch failed as file \"{FileHistory.Text}\"doesn't exist");
        }

        private void CleanUp_Click(object sender, EventArgs e)
        {
            busy.Select();
            ConvertPCBDoc.OutputString("Cleaning Up");
            string UnpackDirectory = FileHistory.Text.Substring(0, FileHistory.Text.LastIndexOf('.')) + "-Kicad";
            if (Directory.Exists(UnpackDirectory))
            {
                ConvertPCBDoc.OutputString($"Removing \"{UnpackDirectory}\"'s contents");
                ConvertPCBDoc.ClearFolder(UnpackDirectory);
                ConvertPCBDoc.OutputString($"Deleting {UnpackDirectory}");
                Directory.Delete(UnpackDirectory);
            }
            else
            {
                ConvertPCBDoc.OutputError("Output directory doesn't exist");
            }
        }

        private void FileHistory_SelectedIndexChanged(object sender, EventArgs e)
        {
            FileHistory.Text = FileHistory.Text;
        }

        private void ClearHistory_Click(object sender, EventArgs e)
        {
            busy.Select();
            FileHistory.Items.Clear();
            FileHistory.ResetText();
            FileHistory.Items.Insert(0, FileHistory.Text);
            FileHistory.SelectedIndex = 0;
        }

        private void Edit_Click(object sender, EventArgs e)
        {
            busy.Select();
            Process p = new Process();
            if (FileHistory.Text != "" && File.Exists(FileHistory.Text))
            {
                string UnpackDirectory = FileHistory.Text.Substring(0, FileHistory.Text.LastIndexOf('.')) + "-Kicad";
                if (Directory.Exists(UnpackDirectory))
                {
                    int index = FileHistory.Text.LastIndexOf('.');
                    string FileName;
                    FileName = FileHistory.Text.Substring(FileHistory.Text.LastIndexOf('\\') + 1);
                    FileName = FileName.Substring(0, FileName.LastIndexOf('.'));

                    string output_filename = UnpackDirectory + "\\" + FileName + ".kicad_pcb";

                    if (File.Exists(output_filename))
                    {
                        try
                        {
                            p.StartInfo = new ProcessStartInfo("C:\\Program Files\\Notepad++\\notepad++.exe", "\"" + output_filename + "\"");
                            p.StartInfo.RedirectStandardOutput = false;
                            p.StartInfo.RedirectStandardError = true;
                            p.StartInfo.UseShellExecute = false;
                            p.Start();
                        }
                        catch (System.ComponentModel.Win32Exception)
                        {
                            ConvertPCBDoc.OutputError("Couldn't launch PcbNew");
                        }
                    }
                }
            }
        }

        private void FileHistory_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete && FileHistory.DroppedDown)
            {
                //Remove the listitem from the combobox list
                FileHistory.Items.RemoveAt(FileHistory.SelectedIndex);

                //Make sure no other processing happens, ex: deleting text from combobox
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Down && !FileHistory.DroppedDown)
            {
                //If the down arrow is pressed show the dropdown list from the combobox
                FileHistory.DroppedDown = true;

            }
        }
    }
}

