using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ConvertToKicad
{
    public partial class ConvertPCBDoc
    {
        // class for the 3D models
        class Model
        {
            public string FileName { get; set; }
            public string ID { get; set; }
            private readonly UInt32 Component;
            private readonly string Checksum;
            private readonly double ROTX, ROTY, ROTZ, DZ;

            Model()
            {
            }

            public Model(string line, UInt32 Number)
            {
                FileName = GetString(line, "NAME=");
                ID = GetString(line, "ID=");
                Component = Number;
                Checksum = GetString(line, "CHECKSUM=");
                // rename file to include the checksum
                FileName = System.IO.Path.GetFileNameWithoutExtension(FileName);
                // append the checksum to cater for same name models with different contents
                FileName += $"{{{Checksum}}}.step";
                ROTX = GetDouble(GetString(line, "ROTX="));
                ROTY = GetDouble(GetString(line, "ROTY="));
                ROTZ = GetDouble(GetString(line, "ROTZ="));
                DZ = GetDouble(GetString(line, "DZ="));
            }
        }

        class Models
        {
            public List<Model> Mods;

            public Models()
            {
                Mods = new List<Model>();
            }

            public void Add(Model Model)
            {
                if (Model != null)
                    Mods.Add(Model);
            }

            // get the model filename
            public string GetFilename(string ID)
            {
                foreach (var Mod in Mods)
                {
                    if (Mod.ID == ID)
                        return Mod.FileName;
                }
                return "";
            }
        }

        static Models Mods;

        static bool ProcessModelsFile(string filename)
        {
            FileInfo file = new System.IO.FileInfo(filename);
            long size = new System.IO.FileInfo(filename).Length;

            using (FileStream fs = file.OpenRead())
            {
                UInt32 pos = 0;
                UInt32 Component = 0;
                BinaryReader br = new BinaryReader(fs, System.Text.Encoding.UTF8);
                while (pos < size)
                {
                    fs.Seek(pos, SeekOrigin.Begin);
                    uint next = br.ReadUInt32();
                    char[] line = br.ReadChars((int)next);
                    string str = new string(line);
                    Model Model = new Model(str, Component);
                    try
                    {
                        string f = Model.FileName;
                        if (!File.Exists(f))
                            // rename the file
                            File.Move($"{Component}.step", f);
                        else
                            File.Delete($"{Component}.step");
                    }
                    catch (Exception Ex)
                    {
                        CheckThreadAbort(Ex);
                    }
                    pos += next + 4;
                    Component++;
                }
            }
            return true;
        }

        // class for the 3D models document entry in the pcbdoc file
        class Models6 : PcbDocEntry
        {
            public Models6(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {
            }

            public override bool ProcessFile(byte[] data)
            {
                FileInfo file = new System.IO.FileInfo(filename);
                using (MemoryStream ms = new MemoryStream(data))
                {
                    UInt32 pos = 0;
                    long size = ms.Length;

                    UInt32 Component = 0;
                    BinaryReader br = new BinaryReader(ms, System.Text.Encoding.UTF8);
                    while (pos < size)
                    {
                        base.ProcessLine();
                        ms.Seek(pos, SeekOrigin.Begin);
                        uint next = br.ReadUInt32();
                        char[] line = br.ReadChars((int)next);
                        string str = new string(line);
                        Model Model = new Model(str, Component);
                        if (Model != null)
                            Mods.Add(Model);
                        pos += next + 4;
                        Component++;
                    }
                }
                return true;
            }
        }
    }
}
