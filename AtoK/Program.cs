/*
 * This program source code file is part of AtoK
 * Copyright (C) 2020 Stephen Green
 *
 * This program is free software: you can redistribute it and/or modify it
 * under the terms of the GNU General Public License as published by the
 * Free Software Foundation, either version 3 of the License, or (at your
 * option) any later version.
 *
 * This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License along
 * with this program.  If not, see <http://www.gnu.or/licenses/>.
 */

using Ionic.Zlib;
using OpenMcdf;
using System;
using System.Threading;
using System.Collections.Generic;
using System.Windows.Forms;
using System.IO;
using ConvertToKicad;

namespace AtoK
{
    public class Program
    {
        public static Form1 Form;
        public static bool ConsoleApp = false;
        public static ConvertPCBDoc ConvertPCB;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            ConvertPCB = new ConvertPCBDoc();
            ConvertPCBDoc.ExtractFiles = false;
            ConvertPCBDoc.CreateLib = false;
            if (args.Length > 0)
            {
                // run as console app
                ConsoleApp = true;
                // parse command line parameters
                for (var i = 0; i < args.Length; i++)
                {
                    if (args[i].Substring(0, 1) == "-")
                    {
                        // this is a command line option
                        if (args[i] == "-e")
                            ConvertPCBDoc.ExtractFiles = true;
                        if (args[i] == "-l")
                            ConvertPCBDoc.CreateLib = true;
                        if (args[i] == "-v")
                            ConvertPCBDoc.Verbose = true;
                    }
                    else
                        ConvertPCBDoc.filename = args[i];
                }

                if (!File.Exists(ConvertPCBDoc.filename))
                {
                    Console.Error.WriteLine($"File {ConvertPCBDoc.filename} doesn't exist");
                    System.Environment.Exit(0);
                }

                if ((ConvertPCBDoc.filename.Length - ConvertPCBDoc.filename.IndexOf(".pcbdoc", StringComparison.OrdinalIgnoreCase)) != 7)
                {
                    Console.Error.WriteLine($"File {ConvertPCBDoc.filename} should end in '.pcbdoc'");
                    System.Environment.Exit(0);
                }

                int index = ConvertPCBDoc.filename.IndexOf('.');
                ConvertPCBDoc.output_filename = ConvertPCBDoc.filename.Substring(0, index) + ".kicad_pcb";
                if (index == -1)
                {
                    Console.Error.WriteLine($"File {ConvertPCBDoc.filename} is not valid pcb file");
                    System.Environment.Exit(0);
                }

                if (ConvertPCBDoc.filename.Substring(index, ConvertPCBDoc.filename.Length - index).ToLower() != ".pcbdoc")
                {
                    Console.Error.WriteLine($"File {ConvertPCBDoc.filename} is not valid pcb file");
                    System.Environment.Exit(0);
                }

                ConvertPCB.ConvertFile(ConvertPCBDoc.filename, ConvertPCBDoc.ExtractFiles, ConvertPCBDoc.CreateLib);
            }
            else
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Form = new Form1();
                Application.Run(Form);
            }
        }
    }
}