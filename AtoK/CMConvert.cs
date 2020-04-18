using System;
using OpenMcdf;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

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
    //            AppendData($"{newdir}\\data.dat", data);
    //            ClearFolder(orig);
    //            Directory.Delete(orig);
                OutputString($"Deleted {orig}");
                long after = new System.IO.FileInfo($"{newdir}\\data.dat").Length;
                OutputString($"Appended {after - before} bytes to {newdir}\\data.dat");
            }
            else
            {
   //             Directory.Move(orig, newdir);
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

        private bool IsShapeBasedComponentBodies6(byte[] data)
        {
            string str = Encoding.Default.GetString(data);
            return str.Contains("MODEL.CHECKSUM=");

            TypeBinary typebinary = ByteArrayToStructure<TypeBinary>(data);
            if (typebinary.Type != 0x0C)
                return false;
            if (typebinary.Next+5 > data.Length)
                return false;
            if (typebinary.Next + 5 == data.Length)
                return true;
            if (data[typebinary.Next + 5] != 0x0C)
                return false;
            return true;
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
            return str.Contains("STANDOFFHEIGHT=");
        }

        private bool IsModels6(byte[] data)
        {
            string str = Encoding.Default.GetString(data);
            return str.Contains("EMBED=");
        }

        private bool IsBoard6(byte[] data)
        {
            string str = Encoding.Default.GetString(data);
            return str.Contains("ORIGINX=");
        }

        private bool IsPolygons6(byte[] data)
        {
            string str = Encoding.Default.GetString(data);
            return str.Contains("REMOVEISLANSDBYAREA=");
        }

        private bool IsArcs6(byte[] data)
        {
            TypeBinary typebinary = ByteArrayToStructure<TypeBinary>(data);
            if (typebinary.Type != 1)
                return false;
            if (typebinary.Next > data.Length)
                return false;
            if (data[typebinary.Next + 5] != 1)
                return false;
            return true;
        }

        private bool IsTexts6(byte[] data)
        {
            // check to see if header is the Texts file
            using (MemoryStream ms = new MemoryStream(data))
            {
                UInt32 pos = 0;
                uint size = (uint)ms.Length;

                BinaryReader br = new BinaryReader(ms, System.Text.Encoding.UTF8);
                ms.Seek(pos, SeekOrigin.Begin);
                List<UInt32> Starts = new List<UInt32>();
                // signature is
                // byte 05
                // int32 length
                // then at position length+5 is UInt32 length of string
                // then at position length + strlen is byte 5
                ms.Seek(pos, SeekOrigin.Begin);
                byte recordtype = br.ReadByte(); // should be 5
                if (recordtype != 5)
                    return false;
                else
                {
                    // possible start
                    UInt32 textaddr = br.ReadUInt32()+5;
                    if (textaddr>=data.Length)
                        return false;
                    else
                    {
                        ms.Seek(textaddr, SeekOrigin.Begin);
                        UInt32 textlength = br.ReadUInt32();
                        if(textaddr+textlength < data.Length)
                        {
                            ms.Seek(textaddr+textlength+4, SeekOrigin.Begin);
                            byte nexttype = br.ReadByte();
                            if (nexttype == 5)
                                return true;
                        }
                    }
                }
            }
            return false;
        }

        private bool IsDimensions6(byte[] data)
        {
            string str = Encoding.Default.GetString(data);
            return str.Contains("DIMENSIONLAYER=");
        }

        private bool IsRules6(byte[] data)
        {
            string str = Encoding.Default.GetString(data);
            return str.Contains("RULEKIND=");
        }

        void CMConvert(CompoundFile cf)
        {
            IList<IDirectoryEntry> entries = cf.GetDirectories();

            // extract Circuit maker files and then try and rename directories
            // to match .pcbdoc entries
            string CurrentDir = "";
            foreach (var entry in entries)
            {
                if (false && entry.Name == "Root Entry")
                {
                    DirectoryInfo Info = Directory.CreateDirectory(entry.Name);
                    if(!Info.Exists)
                        // for some reason the create directory failed so try again
                        Info = Directory.CreateDirectory(entry.Name);
                    Thread.Sleep(500); // TODO this is a frig sort out
                    ClearFolder(entry.Name);
                    Directory.SetCurrentDirectory(entry.Name);
                    CurrentDir = entry.Name;
                }
                else
                {
                    if (entry.StgType == StgType.StgStorage)
                    {
                        CurrentDir = entry.Name;
      //                  Directory.CreateDirectory(entry.Name);
                        CFStorage storage = cf.RootStorage.TryGetStorage(entry.Name);
                        CFStream datastream = storage.GetStream("Data");
                        // get file contents and write to file
                        byte[] data = datastream.GetData();
                        if (data.Length == 0)
                        {
                            OutputString($"Deleted '{CurrentDir}' no data");
                            // remove empty directory
       //                     Directory.Delete(CurrentDir);
                        }
                        else
                        {
                            // create the file
    //                        File.WriteAllBytes(CurrentDir + "\\" + "Data.dat", data);
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
                                RenameDirectory(CurrentDir, "Tracks6", data);
                            else
                            if (IsComponentBodies(data))
                                RenameDirectory(CurrentDir, "ComponentBodies6", data);
                            else
                            if (IsBoard6(data))
                                RenameDirectory(CurrentDir, "Board6", data);
                            else
                            if (IsPolygons6(data))
                                RenameDirectory(CurrentDir, "Polygons6", data);
                            else
                            if (IsShapeBasedComponentBodies6(data))
                                RenameDirectory(CurrentDir, "ShapeBasedComponentBodies6", data);
                            else
                            if (IsArcs6(data))
                                RenameDirectory(CurrentDir, "Arcs6", data);
                            else
                            if (IsTexts6(data))
                                RenameDirectory(CurrentDir, "Texts6", data);
                            else
                            if (IsDimensions6(data))
                                RenameDirectory(CurrentDir, "Dimensions6", data);
                            else
                            if (IsRules6(data))
                                RenameDirectory(CurrentDir, "Rules6", data);
                            else
                            if (IsModels6(data))
                            {
                                RenameDirectory(CurrentDir, "Models", data);
                                // now need to get all of the model files
                                CFStream models;
                                byte[] modeldata;
                                int i = 0;
                                while ((models = storage.TryGetStream($"{i}")) != null)
                                {
                                    OutputString($"Creating {i}.dat model file");
                                    // get file contents and write to file
                                    modeldata = models.GetData();
                                    // uncompress the x.dat file to a .step file
                                    // step file is renamed to it's actual name later in the process
                   //                 string Inflated = ZlibCodecDecompress(modeldata);
                   //                 File.WriteAllText($"Models\\{i}.step", Inflated);
                                    i++;
                                }
                            }
                            else
                            if ((textfile2.type == 0x0001 && textfile2.Length < data.Length) || textfile.Length < data.Length)
                            {
                                // could be text file
                                string str = Encoding.Default.GetString(data);
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
                                    RenameDirectory(CurrentDir, "Components6", data);
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
                                else
                                if (typebinary.Type == 0x04)
                                {
                                    RenameDirectory(CurrentDir, "Tracks6", data);
                                }
                                else
                                if (typebinary.Type == 0x0B)
                                {
                                    RenameDirectory(CurrentDir, "Regions6", data);
                                }
                                else
                                if (typebinary.Type == 0x06)
                                {
                                    RenameDirectory(CurrentDir, "Fills6", data);
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