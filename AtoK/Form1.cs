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
using System.Reflection;
using Ionic.Zlib;

namespace AtoK
{
    public partial class Form1 : Form
    {
        public enum CleanEnum
        {
            None,
            Current,
            All
        }

        public static string PcbnewLocation { get; set;}
        public static string TextEditorLoc { get; set; }
        public static CleanEnum CleanFlag;
        ConcurrentQueue<string> dq = new ConcurrentQueue<string>();
        public static Thread t; // used for running the convert in  the background
        private delegate void UpdateOutputDelegate(string s, Color colour);

        private UpdateOutputDelegate updateoutputDelegate = null;

        public void UpdateOutput(string s, Color colour)
        {
            OutputList_Add(s, colour);
            OutputList.Update();
        }

        public void SetTextEditorLocation(string Location)
        {
            TextEditorLoc = Location;
            Properties.Settings.Default.TextEditorLocation = Location;
            Properties.Settings.Default.Save();
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
        Size MaximisedSize;

        public Form1()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                string resourceName = new AssemblyName(args.Name).Name + ".dll";
                string resource = Array.Find(this.GetType().Assembly.GetManifestResourceNames(), element => element.EndsWith(resourceName));

                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
                {
                    Byte[] assemblyData = new Byte[stream.Length];
                    stream.Read(assemblyData, 0, assemblyData.Length);
                    return Assembly.Load(assemblyData);
                }
            };

            Screen scrn = Screen.FromControl(this);

            InitializeComponent();
            MaximisedSize = new Size(0, 0);

            OutputList_Initialize();
            FileHistory_Initialize();
            SaveExtractedDocs.CheckState = Properties.Settings.Default.SaveDocs ? CheckState.Checked : CheckState.Unchecked;
            LibraryGen.CheckState        = Properties.Settings.Default.GenLib ? CheckState.Checked : CheckState.Unchecked;
            Verbose.CheckState           = Properties.Settings.Default.Verbose ? CheckState.Checked : CheckState.Unchecked;
            FileHistory.Text             = Properties.Settings.Default.LastFile;
            FileHistory.Select(FileHistory.Text.Length, 0); // scroll to make filename visible

            ComboboxItems = Properties.Settings.Default.ComboboxItems;
            string[] Items = ComboboxItems.Split(';');
            foreach (var item in Items)
            {
                if (item != "")
                    FileHistory.Items.Insert(0, item);
            }
            FileHistory.SelectedIndex = (ComboboxItems=="")?-1:Properties.Settings.Default.ComboBoxIndex;
            Properties.Settings.Default.ComboBoxIndex = FileHistory.SelectedIndex;
            Properties.Settings.Default.Save();
            PcbnewLocation = "";
            TextEditorLoc = "";
        }

        private void LinkIonicZlib()
        {
            Ionic.Zlib.CompressionLevel x;
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
                if (this.WindowState == FormWindowState.Minimized)
                    this.WindowState = FormWindowState.Normal;

                this.Location = Properties.Settings.Default.F1Location;
                if (Properties.Settings.Default.F1Size.Width < Edit.Width + Edit.Location.X + 25)
                    // n.b. this calls the Form1_Resize
                    this.Size = new Size(Edit.Right + 28, Properties.Settings.Default.F1Size.Height+5);
                else
                    // n.b. this calls the Form1_Resize
                    this.Size = Properties.Settings.Default.F1Size; // TODO sort out when this gets screwed 
                this.Update();
                MaximisedSize = this.Size;
            }
            SaveExtractedDocs.CheckState = Properties.Settings.Default.SaveDocs ? CheckState.Checked : CheckState.Unchecked;
            LibraryGen.CheckState        = Properties.Settings.Default.GenLib ? CheckState.Checked : CheckState.Unchecked;
            Verbose.CheckState           = Properties.Settings.Default.Verbose ? CheckState.Checked : CheckState.Unchecked;
            ConvertPCBDoc.Verbose        = Verbose.CheckState == CheckState.Checked;
            string Combo                 = Properties.Settings.Default.ComboboxItems;
            PcbnewLocation               = Properties.Settings.Default.PcbnewLocation;
            TextEditorLoc                = Properties.Settings.Default.TextEditorLocation;

            string[] items = Combo.Split(';');

            // remove any duplicates
            var b = new HashSet<string>(items);

            FileHistory.ContextMenu = ComboBoxMenu;
            FileHistory.Items.Clear();
            FileHistory.Items.AddRange(b.ToArray());
            if (b.ToArray().Length != items.Length)
            {
                // some duplicate items have been removed
                // so selected index will no longer be valid
                FileHistory.SelectedIndex = 0;
            }
            else
                FileHistory.SelectedIndex = Properties.Settings.Default.ComboBoxIndex;

            FileHistory.Text = (string)FileHistory.Items[FileHistory.SelectedIndex];

            EnableControls();
        }

        FormWindowState LastWindowState = FormWindowState.Minimized;

        private void Form1_Resize(object sender, EventArgs e)
        {
            Control control = (Control)sender;
            if (WindowState != LastWindowState)
            {
                LastWindowState = WindowState;

                if (WindowState == FormWindowState.Maximized)
                {
                    Size = MaximisedSize;
                    OutputList.Width = ClearHistory.Right - SelectSource.Left;
                    OutputList.Left = FileHistory.Left;
                    OutputList.Top = Verbose.Bottom + 10;
                    OutputList.Height = control.Height - Verbose.Bottom - 60;
                }
            }
            if (WindowState == FormWindowState.Normal)
            {
                this.MinimumSize = new Size(this.Width, 0);
                this.MaximumSize = new Size(this.Width, Int32.MaxValue);
                // resize the output window 
                // set top to bottom of button1
                // set bottom to bottom of form
                // set left to left of form
                // set right to right of form
                OutputList.Width = ClearHistory.Right - SelectSource.Left;
                OutputList.Left = FileHistory.Left;
                OutputList.Top = Verbose.Bottom + 10;
                OutputList.Height = control.Height - Verbose.Bottom - 60;
                if (OutputList.Height < OutputList.ItemHeight)
                {
                    OutputList.Height = OutputList.ItemHeight;
                    this.Height = OutputList.Bottom + 50;
                }
                MaximisedSize = this.Size;
            }

        }

        private void Form1_Closing(object sender, FormClosingEventArgs e)
        {
            if (t != null && t.IsAlive)
            {
                t.Abort();
                //for(var i = 0; i < 10; i++)
                //    if 
                while(ConvertPCBDoc.ConvertRunning)
                    Thread.Sleep(1000);
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
            Properties.Settings.Default.SaveDocs           = SaveExtractedDocs.CheckState == CheckState.Checked;
            Properties.Settings.Default.GenLib             = LibraryGen.CheckState == CheckState.Checked;
            Properties.Settings.Default.Verbose            = Verbose.CheckState == CheckState.Checked;
            Properties.Settings.Default.LastFile           = FileHistory.Text;
            Properties.Settings.Default.PcbnewLocation     = PcbnewLocation;
            Properties.Settings.Default.TextEditorLocation = TextEditorLoc;
            SaveFileHistory();
            // don't forget to save the settings
            Properties.Settings.Default.Save();
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

        public void OutputList_Add(string Str, Color color)
        {
            Line newLine;
            Invoke((MethodInvoker)(() => OutputList.BeginUpdate()));
            string[] strings = Str.Split('\n');
            foreach (var str in strings)
            {
                newLine = new Line(str, color);
                lines.Add(newLine);
                int testWidth = TextRenderer.MeasureText(str,
                                                OutputList.Font, OutputList.ClientSize,
                                                TextFormatFlags.NoPrefix).Width;
                if (testWidth > outputlist_width)
                    outputlist_width = testWidth;

                Invoke((MethodInvoker)(() => OutputList.HorizontalExtent = outputlist_width));
                Invoke((MethodInvoker)(() => OutputList.Items.Add(newLine)));
                Invoke((MethodInvoker)(() => OutputList_Scroll()));
                Invoke((MethodInvoker)(() => OutputList.EndUpdate()));
            }
        }

        public void OutputList_Update()
        {
            Invoke((MethodInvoker)(() => OutputList.Update()));
        }

        private void OutputList_Initialize()
        {
            // owner draw for listbox so we can add color
            OutputList.DrawMode = DrawMode.OwnerDrawFixed;
            OutputList.DrawItem += new DrawItemEventHandler(OutputList_DrawItem);
            OutputList.ClearSelected();

            // build the outputList context menu
            popUpMenu = new ContextMenu();
            popUpMenu.MenuItems.Add(new MenuItem("Delete selected",  OutputList_DeleteSelected, Shortcut.CtrlX));
            popUpMenu.MenuItems.Add(new MenuItem("Clear All",        OutputList_ClearAll));
            popUpMenu.MenuItems.Add(new MenuItem("Select &All",      OutputList_SelectAll,      Shortcut.CtrlA));
            popUpMenu.MenuItems.Add(new MenuItem("&Copy",            OutputList_Copy,           Shortcut.CtrlC));
            popUpMenu.MenuItems.Add(new MenuItem("Copy All",         OutputList_CopyAll));
            popUpMenu.MenuItems.Add(new MenuItem("Unselect",         OutputList_ClearSelected));
            popUpMenu.MenuItems.Add(new MenuItem("Toggle Scrolling", OutputList_ToggleScrolling));

            // despite the following the first item in the menu show up as "Clear All    Ctrl+X"
            popUpMenu.MenuItems[0].ShowShortcut = false;

            OutputList.ContextMenu = popUpMenu;
        }

        void OutputList_DrawItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index >= 0 && e.Index < OutputList.Items.Count)
            {
                Line line = (Line)OutputList.Items[e.Index];

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

        void OutputList_Scroll()
        {
            if (scrolling)
            {
                int itemsPerPage = (int)(OutputList.Height / OutputList.ItemHeight);
                OutputList.TopIndex = OutputList.Items.Count - itemsPerPage;
                if (OutputList.HorizontalScrollbar && (itemsPerPage < OutputList.Items.Count))
                    OutputList.TopIndex++;
            }
        }

        private void OutputList_SelectedIndexChanged(object sender, EventArgs e)
        {
            popUpMenu.MenuItems[0].Enabled = (OutputList.SelectedItems.Count > 0);
        }

        private void OutputList_Copy()
        {
            int iCount = OutputList.SelectedItems.Count;
            if (iCount > 0)
            {
                String[] source = new String[iCount];
                for (int i = 0; i < iCount; ++i)
                {
                    source[i] = ((Line)OutputList.SelectedItems[i]).Str;
                }

                String dest = String.Join("\r\n", source);
                Clipboard.SetText(dest);
            }
        }

        private void OutputList_Copy(object sender, EventArgs e)
        {
            OutputList_Copy();
        }

        private void OutputList_CopyAll(object sender, EventArgs e)
        {
            int iCount = OutputList.Items.Count;
            if (iCount > 0)
            {
                String[] source = new String[iCount];
                for (int i = 0; i < iCount; ++i)
                {
                    source[i] = ((Line)OutputList.Items[i]).Str;
                }

                String dest = String.Join("\r\n", source);
                Clipboard.SetText(dest);
            }
        }

        private void OutputList_SelectAll(object sender, EventArgs e)
        {
            OutputList.BeginUpdate();
            for (int i = 0; i < OutputList.Items.Count; ++i)
            {
                OutputList.SetSelected(i, true);
            }
            OutputList.EndUpdate();
        }

        private void OutputList_ClearSelected(object sender, EventArgs e)
        {
            OutputList.ClearSelected();
            OutputList.SelectedItem = -1;
        }

        private void OutputList_DeleteSelected(object sender, EventArgs e)
        {
            OutputList.BeginUpdate();
            var selectedIndices = new List<int>(OutputList.SelectedIndices.Cast<int>());
            // but first copy selected text to the clipboard
            var Selected = new StringBuilder("");
            selectedIndices.ForEach(index => Selected.Append(((Line)OutputList.Items[index]).Str + "\n"));
            String dest = String.Join("\r\n", Selected.ToString());
            if (dest != "")
            {
                Clipboard.SetText(dest);
                // now delete the items
                // Remove each item in reverse order to maintain integrity
                selectedIndices.Reverse();
                selectedIndices.ForEach(index => OutputList.Items.RemoveAt(index));

                OutputList.SelectedItem = -1;
            }
            OutputList.EndUpdate();
        }

        private void OutputList_ClearAll(object sender, EventArgs e)
        {
            OutputList.Items.Clear();
            OutputList.SelectedItem = -1;
        }

        #endregion

        #region User interaction

        /// <summary>
		/// toggle scrolling
		/// </summary>
		private void OutputList_ToggleScrolling(object sender, EventArgs e)
        {
            scrolling = !scrolling;
            OutputList_Scroll();
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

        private void ConvertCancel_Click(object sender, EventArgs e)
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
                CheckOutputDir();
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
                SelectSource.Enabled = false;
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
                OutputList_Add($"File \"{FileHistory.Text}\" doesn't exist", System.Drawing.Color.Red);
        }

        public void EnableControls()
        {
            SaveExtractedDocs.CheckState = Properties.Settings.Default.SaveDocs ? CheckState.Checked : CheckState.Unchecked;
            LibraryGen.CheckState = Properties.Settings.Default.GenLib ? CheckState.Checked : CheckState.Unchecked;
            Verbose.CheckState = Properties.Settings.Default.Verbose ? CheckState.Checked : CheckState.Unchecked;

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
            SelectSource.Enabled = true;
            SelectSource.Update();
            CleanUp.Enabled = true;
            CleanUp.Update();
            LaunchPCBNew.Enabled = (PcbnewLocation!="" && FileHistory.Items.Count != 0)?true:false;
            LaunchPCBNew.Update();
            Edit.Enabled = (TextEditorLoc != "" && FileHistory.Items.Count != 0) ? true : false;
            Edit.Update();
            ClearHistory.Enabled = FileHistory.Items.Count != 0;
            ClearHistory.Update();
        }

        private void SelectSource_Click(object sender, EventArgs e)
        {
            busy.Select();
            OpenFileDialog openFileDialog1 = new OpenFileDialog
            {

                InitialDirectory = @"D:\",
                Title = "Browse Altium PCBDoc Files",
                CheckFileExists = true,
                CheckPathExists = true,

                DefaultExt = "pcbdoc",
                Filter = "Altium PCB files (*.pcbdoc, *.cmpcbdoc)|*.pcbdoc; *.cmpcbdoc",
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
                    Properties.Settings.Default.LastFile = FileHistory.Text;
                    Properties.Settings.Default.Save();
                }
                SaveFileHistory();
                FileHistory.SelectedItem = 0;
                FileHistory.Select(FileHistory.Text.Length, 0); // scroll to make filename visible
            }
            CheckOutputDir();
        }

        private void Verbose_Click(object sender, EventArgs e)
        {
            ConvertPCBDoc.Verbose = Verbose.Checked;
            Properties.Settings.Default.Verbose = Verbose.CheckState == CheckState.Checked;
            // don't forget to save the settings
            Properties.Settings.Default.Save();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (button1.BackColor == Color.Green)
                button1.BackColor = Color.FromArgb(224, 224, 224);
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
                button1.BackColor = Color.FromArgb(224, 224, 224);
                this.Update();
                if (Globals.ReportLines != 0 && Properties.Settings.Default.ShowWarningsDialog)
                {
                    if (File.Exists(Form1.TextEditorLoc))
                    {
                        if (ShowWarnings() == DialogResult.OK)
                        {
                            var p = new Process
                            {
                                StartInfo = new ProcessStartInfo(Form1.TextEditorLoc, "\"" + Globals.ReportFilename + "\"")
                                {
                                    RedirectStandardOutput = false,
                                    RedirectStandardError = true,
                                    UseShellExecute = false
                                }
                            };
                            p.Start();
                        }
                    }
                    else
                    {
                        ConvertPCBDoc.OutputString($"No editor set, warnings can be found in {Globals.ReportFilename}");
                    }
                }
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
            var p = new Process();
            if (FileHistory.Text != "" && File.Exists(FileHistory.Text))
            {
                string filename = FileHistory.Text;
                string CM = (filename.Contains("CMPCBDoc")) ? "-CM" : "";
                string UnpackDirectory = filename.Substring(0, filename.LastIndexOf('.')) + CM + "-Kicad";
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
                            if (File.Exists(PcbnewLocation))
                            {
                                p.StartInfo = new ProcessStartInfo(PcbnewLocation, "\"" + output_filename + "\"")
                                {
                                    RedirectStandardOutput = false,
                                    RedirectStandardError = true,
                                    UseShellExecute = false
                                };
                                p.Start();
                            }
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

        private void CleanOutput(string filename)
        {
            if (busy != null) busy.Select();
            string CM = (filename.Contains("CMPCBDoc")) ? "-CM" : "";
            string UnpackDirectory = filename.Substring(0, filename.LastIndexOf('.')) + CM + "-Kicad";
            if (Directory.Exists(UnpackDirectory))
            {
                ConvertPCBDoc.OutputString($"Removing \"{UnpackDirectory}\"'s contents");
                ConvertPCBDoc.ClearFolder(UnpackDirectory);
                ConvertPCBDoc.OutputString($"Deleting \"{UnpackDirectory}\"");
                try
                {
                    Directory.Delete(UnpackDirectory);
                }
                catch(Exception Ex)
                {
                }
            }
            CheckOutputDir();
        }

        private void CleanUpAll()
        {
            ConvertPCBDoc.OutputString("Removing generated content");

            for (var i=0;i< FileHistory.Items.Count; i++)
            {
                CleanOutput(FileHistory.GetItemText(FileHistory.Items[i]));
            }
            ConvertPCBDoc.OutputString("Finished");
            CheckOutputDir();
        }

        private void CleanUp_Click(object sender, EventArgs e)
        {
            var Clean = new CleanUp
            {
                StartPosition = System.Windows.Forms.FormStartPosition.Manual
            };
            Clean.Location = new System.Drawing.Point((this.Location.X + this.Width / 2) - (Clean.Width / 2), (this.Location.Y + this.Height / 2) - (Clean.Height / 2));
            Clean.ShowDialog();
            switch (CleanFlag)
            {
                case CleanEnum.None: break;
                case CleanEnum.Current: CleanOutput(FileHistory.Text); break;
                case CleanEnum.All: CleanUpAll();
                    break;
            }
        }

        private void Edit_Click(object sender, EventArgs e)
        {
            busy.Select();
            var p = new Process();
            if (FileHistory.Text != "" && File.Exists(FileHistory.Text))
            {
                string UnpackDirectory = FileHistory.Text.Substring(0, FileHistory.Text.LastIndexOf('.')) + "-Kicad";
                if (!Directory.Exists(UnpackDirectory))
                {
                    ConvertPCBDoc.OutputError($"Directory \"{UnpackDirectory}\" doesn't exist");
                }
                else
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
                            if (File.Exists(TextEditorLoc))
                            {
                                p.StartInfo = new ProcessStartInfo(TextEditorLoc, "\"" + output_filename + "\"")
                                {
                                    RedirectStandardOutput = false,
                                    RedirectStandardError = true,
                                    UseShellExecute = false
                                };
                                p.Start();
                            }
                        }
                        catch (System.ComponentModel.Win32Exception)
                        {
                            ConvertPCBDoc.OutputError("Couldn't launch PcbNew");
                        }
                    }
                }
            }
        }

        // File History control handling
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

        private void SaveFileHistory()
        {
            var sb = new StringBuilder();
            foreach (var item in FileHistory.Items)
            {
                if (item.ToString() != "")
                    sb.Append(item.ToString() + ";");
            }
            string Items = sb.ToString();
            if (Items != "")
            {
                char[] charsToTrim = { ';' };
                Items = Items.Substring(0, Items.Length - 1);
            }
            Properties.Settings.Default.ComboboxItems = Items;
            Properties.Settings.Default.ComboBoxIndex = FileHistory.SelectedIndex;
            Properties.Settings.Default.Save();
            EnableControls();

        }

        void CheckOutputDir()
        {
            if(FileHistory.Text=="")
            {
                Edit.Enabled = false;
                return;
            }
            string UnpackDirectory = FileHistory.Text.Substring(0, FileHistory.Text.LastIndexOf('.')) + "-Kicad";
            LaunchPCBNew.Enabled = Directory.Exists(UnpackDirectory) && PcbnewLocation != "";
            Edit.Enabled = Directory.Exists(UnpackDirectory) && TextEditorLoc != "";
        }

        private void FileHistory_SelectedIndexChanged(object sender, EventArgs e)
        {
            FileHistory.Text = (string)FileHistory.Items[FileHistory.SelectedIndex];
            Properties.Settings.Default.ComboBoxIndex = FileHistory.SelectedIndex;
            Properties.Settings.Default.Save();
            CheckOutputDir();
        }

        private void FileHistory_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete && FileHistory.DroppedDown)
            {
                //Remove the listitem from the combobox list
                FileHistory.Items.RemoveAt(FileHistory.SelectedIndex);
                if(FileHistory.SelectedIndex > FileHistory.Items.Count)
                {
                    FileHistory.SelectedIndex = 0;
                }
                SaveFileHistory();

                //Make sure no other processing happens, ex: deleting text from combobox
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Down && !FileHistory.DroppedDown)
            {
                //If the down arrow is pressed show the dropdown list from the combobox
                FileHistory.DroppedDown = true;

            }
        }

        private void FileHistory_MouseClick(object sender, MouseEventArgs e)
        {
            if(e.Button == MouseButtons.Right)
                ComboBoxMenu.Show(FileHistory, e.Location);
        }

        private void ClearHistory_Click(object sender, EventArgs e)
        {
            busy.Select();
            FileHistory.Items.Clear();
            FileHistory.ResetText();
//            FileHistory.Items.Insert(0, FileHistory.Text);
            FileHistory.SelectedIndex = -1;
            SaveFileHistory();
        }

        private void Options_Click(object sender, EventArgs e)
        {
            var OptionsForm = new Options
            {
                StartPosition = System.Windows.Forms.FormStartPosition.Manual
            };
            OptionsForm.Location = new System.Drawing.Point((this.Location.X + this.Width / 2) - (OptionsForm.Width / 2), (this.Location.Y + this.Height / 2) - (OptionsForm.Height / 2));
            OptionsForm.ShowDialog();
            EnableControls();
        }

        public DialogResult ShowWarnings()
        {
            var Form = new Warnings
            {
                StartPosition = System.Windows.Forms.FormStartPosition.Manual
            };
            Form.Location = new System.Drawing.Point((this.Location.X + this.Width / 2) - (Form.Width / 2), (this.Location.Y + this.Height / 2) - (Form.Height / 2));
            return Form.ShowDialog();
        }

        private void outputList_KeyPress(object sender, KeyPressEventArgs e)
        {
            char key = e.KeyChar;
            switch((int)key)
            {
                case 3: OutputList_Copy(); break;
                default: break;
            }
        }
    }
}

