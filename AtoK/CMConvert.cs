using System;
using OpenMcdf;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ConvertToKicad
{

    public partial class ConvertPCBDoc
    {
        private static void AppendData(string filename, byte[] Data)
        {
            using (var fileStream = new FileStream(filename, FileMode.Append, FileAccess.Write, FileShare.None))
            using (var bw = new BinaryWriter(fileStream))
            {
                bw.Write(Data);
            }
        }

        private void RenameDirectory(string orig, string newdir, byte[] data)
        {
            if (Directory.Exists(newdir))
            {
                // directory already exists so merge data onto existing file
                OutputError($"renaming '{orig}' failed as '{newdir}' already exists");
                long before = new System.IO.FileInfo($"{newdir}\\data.dat").Length;
                AppendData($"{newdir}\\data.dat", data);
                long after = new System.IO.FileInfo($"{newdir}\\data.dat").Length;
                OutputString($"Appended {after - before} bytes");
            }
            else
            {
                Directory.Move(orig, newdir);
                OutputString($"Renamed {orig} to '{newdir}");
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public unsafe struct TextFile
        {
            public UInt32 Length;
        }

        public unsafe struct TextFile2
        {
            public UInt16 type;
            public UInt32 Length;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public unsafe struct TypeBinary
        {
            public byte Type;
            public UInt32 Next;
        }

        private bool IsPads(byte[] data)
        {
            bool found = true;

            // check to see if header is the Pads file
            using (MemoryStream ms = new MemoryStream(data))
            {
                UInt32 pos = 0;
                uint size = (uint)ms.Length;

                BinaryReader br = new BinaryReader(ms, System.Text.Encoding.UTF8);
                ms.Seek(pos, SeekOrigin.Begin);
                List<UInt32> Starts = new List<UInt32>();
                // signature is
                // byte 02
                // int32 length
                // byte strlen = length - 1
                // string strlen ascci chars
                // bytes 01 00 00 00
                ms.Seek(pos, SeekOrigin.Begin);
                byte recordtype = br.ReadByte(); // should be 2
                if (recordtype != 2)
                    return false;
                else
                {
                    // possible start
                    UInt32 nextnamelength = br.ReadUInt32();
                    if (nextnamelength > 255)
                        return false;
                    else
                    {
                        uint l = br.ReadByte(); // string length
                        if (l != nextnamelength - 1)
                            return false;
                        else
                        {
                            // everything ok so far
                            // now check bytes in string are ASCII
                            for (int i = 0; i < l; i++)
                            {
                                byte c = br.ReadByte();
                                if (c < 0x20 || c > 0x7F)
                                    found = false;
                            }
                            if (br.ReadByte() != 1)
                                found = false;
                            if (br.ReadByte() != 0)
                                found = false;
                            if (br.ReadByte() != 0)
                                found = false;
                            if (br.ReadByte() != 0)
                                found = false;
                            if (br.ReadByte() != 0)
                                found = false;
                        }
                    }
                }
            }
            return found;
        }


        private bool IsVias(byte[] data)
        {
            TypeBinary typebinary = ByteArrayToStructure<TypeBinary>(data);
            if (typebinary.Type != 3)
                return false;
            if (typebinary.Next > data.Length)
                return false;
            if (data[typebinary.Next + 5] != 3)
                return false;
            return true; 
        }

        private bool IsTracks(byte[] data)
        {
            TypeBinary typebinary = ByteArrayToStructure<TypeBinary>(data);
            if (typebinary.Type != 4)
                return false;
            if (typebinary.Next > data.Length)
                return false;
            if (data[typebinary.Next + 5] != 4)
                return false;
            return true;
        }

        private bool IsComponentBodies(byte[] data)
        {
            string str = Encoding.Default.GetString(data);
            return str.Contains("MODEL.CHECKSUM=");
        }

        void CMConvert(CompoundFile cf)
        {
            IList<IDirectoryEntry> entries = cf.GetDirectories();

            // extract Circuit maker files and then try and rename directories
            // to match .pcbdoc entries
            string CurrentDir = "";
            foreach (var entry in entries)
            {
                if (entry.Name == "Root Entry")
                {
                    if (!Directory.Exists(entry.Name))
                    {
                        Directory.CreateDirectory(entry.Name);
                    }
                    ClearFolder(entry.Name);
                    Directory.SetCurrentDirectory(entry.Name);
                    CurrentDir = entry.Name;
                }
                else
                {
                    if (entry.StgType == StgType.StgStorage)
                    {
                        CurrentDir = entry.Name;
                        Directory.CreateDirectory(entry.Name);
                        CFStorage storage = cf.RootStorage.TryGetStorage(entry.Name);
                        CFStream datastream = storage.GetStream("Data");
                        // get file contents and write to file
                        byte[] data = datastream.GetData();
                        if (data.Length == 0)
                        {
                            OutputString($"Deleted '{CurrentDir}' no data");
                            // remove empty directory
                            Directory.Delete(CurrentDir);
                        }
                        else
                        {
                            // create the file
                            File.WriteAllBytes(CurrentDir + "\\" + "Data.dat", data);
                            // now try and determine which file it is by examining the contents
                            TextFile textfile = ByteArrayToStructure<TextFile>(data);
                            TypeBinary typebinary = ByteArrayToStructure<TypeBinary>(data);
                            TextFile2 textfile2 = ByteArrayToStructure<TextFile2>(data);
                            if (IsPads(data))
                                RenameDirectory(CurrentDir, "Pads6", data);
                            else
                            if (IsVias(data))
                                RenameDirectory(CurrentDir, "Vias6", data);
                            else
                            if (IsTracks(data))
                                RenameDirectory(CurrentDir, "Track6", data);
                            else
                            if (IsComponentBodies(data))
                                RenameDirectory(CurrentDir, "ComponentBodies6", data);
                            else
                            if ((textfile2.type == 0x0001 && textfile2.Length < data.Length) || textfile.Length < data.Length)
                            {
                                // could be text file
                                string str = Encoding.Default.GetString(data);
                                if (str.Contains("DIMENSIONLAYER"))
                                {
                                    RenameDirectory(CurrentDir, "Dimensions6", data);
                                }
                                else
                                if (str.Contains("ORIGINX"))
                                {
                                    RenameDirectory(CurrentDir, "Board6", data);
                                }
                                else
                                if (str.Contains("AdvancedPlacerOptions"))
                                {
                                    RenameDirectory(CurrentDir, "Advanced Placer Options6", data);
                                }
                                else
                                if (str.Contains("SUPERCLASS"))
                                {
                                    RenameDirectory(CurrentDir, "Classes6", data);
                                }
                                else
                                if (str.Contains("SOURCEFOOTPRINTLIBRARY"))
                                {
                                    RenameDirectory(CurrentDir, "Component6", data);
                                }
                                else
                                if (str.Contains("DesignRuleCheckerOptions"))
                                {
                                    RenameDirectory(CurrentDir, "Design Rule Checker Options6", data);
                                }
                                else
                                if (str.Contains("POSITIVENETNAME"))
                                {
                                    RenameDirectory(CurrentDir, "DifferentialPairs6", data);
                                }
                                else
                                if (str.Contains("FWDMSG"))
                                {
                                    RenameDirectory(CurrentDir, "FileVersionInfo", data);
                                }
                                else
                                if (str.Contains("LOOPREMOVAL="))
                                {
                                    RenameDirectory(CurrentDir, "Nets6", data);
                                }
                                else
                                if (str.Contains("PinSwapOptions"))
                                {
                                    RenameDirectory(CurrentDir, "Pin Swap Option6", data);
                                }
                                else
                                if (str.Contains("REMOVEDEAD"))
                                {
                                    RenameDirectory(CurrentDir, "Polygons6", data);
                                }
                                else
                                    OutputError($"Failed to convert possible text file '{CurrentDir}'");
                            }
                            else if (typebinary.Next < data.Length)
                            {
                                if (typebinary.Type == 0x03)
                                {
                                    RenameDirectory(CurrentDir, "Vias6", data);
                                }
                                if (typebinary.Type == 0x04)
                                {
                                    RenameDirectory(CurrentDir, "Track6", data);
                                }
                                if (typebinary.Type == 0x05)
                                {
                                    RenameDirectory(CurrentDir, "Texts6", data);
                                }
                                if (typebinary.Type == 0x0C)
                                {
                                    RenameDirectory(CurrentDir, "Polygons6", data);
                                }
                                if (typebinary.Type == 0x0C)
                                {
                                    RenameDirectory(CurrentDir, "ShapeBasedComponentBodies6", data);
                                }
                                if (typebinary.Type == 0x0B)
                                {
                                    RenameDirectory(CurrentDir, "Regions6", data);
                                }
                                if (typebinary.Type == 0x06)
                                {
                                    RenameDirectory(CurrentDir, "Fills6", data);
                                }
                                if (typebinary.Type == 0x01)
                                {
                                    RenameDirectory(CurrentDir, "Arc6", data);
                                }
                                else
                                    OutputError($"Failed to convert possible binary file '{CurrentDir}'");
                            }
                        }
                    }
                    /*
                    else
                    if (entry.StgType == StgType.StgStream)
                    {
                        CFStream stream = cf.RootStorage.TryGetStream(CurrentDir + "\\" + entry.Name);
                        if (stream != null && stream.Size != 0)
                        {
                            byte[] data = new byte[stream.Size];
                            stream.Read(data, 0, data.Length);
                            File.WriteAllBytes(CurrentDir + "\\" + entry.Name, data);
                        }
                    }
                    */
                }
                //    cf.RootStorage..DirEntry.EntryName.
                //OutputString($"{entry.ToString()}");
            }
            Directory.SetCurrentDirectory("..");
        }
    }
}