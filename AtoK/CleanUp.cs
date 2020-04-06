using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ConvertToKicad;
using System.IO;

namespace AtoK
{
    public partial class CleanUp : Form
    {
        public CleanUp()
        {
            InitializeComponent();
        }

        private void CurrentFile_Click(object sender, EventArgs e)
        {
            Form1.CleanFlag = Form1.CleanEnum.Current;
            Close();
        }

        private void AllFiles_Click(object sender, EventArgs e)
        {
            Form1.CleanFlag = Form1.CleanEnum.All;
            Close();
        }

        private void Cancel_Click(object sender, EventArgs e)
        {
            Form1.CleanFlag = Form1.CleanEnum.None;
            Close();
        }

        private void CleanUp_Load(object sender, EventArgs e)
        {
        }

        private void CleanUp_FormClosing(object sender, FormClosingEventArgs e)
        {

        }

        private void CleanUp_FormClosed(object sender, FormClosedEventArgs e)
        {

        }
    }
}
