using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ConvertToKicad
{
    public partial class ConvertPCBDoc
    {
        class ShapeBasedComponentBodies : PcbDocEntry
        {
            // variable entry size
            public ShapeBasedComponentBodies(string filename, string cmfilename, string record, Type type, int offset) : base(filename, cmfilename, record, type, offset)
            {
                Binary_size = 1;
            }

            public override bool ProcessBinaryFile(byte[] data)
            {
                StartTimer();
                if (Binary_size == 0)
                    return false;

                using (MemoryStream ms = new MemoryStream(data))
                {
                    UInt32 pos = 0;
                    long size = ms.Length;
                    BinaryReader br = new BinaryReader(ms, System.Text.Encoding.UTF8);
                    while (pos < size)
                    {
                        ms.Seek(pos, SeekOrigin.Begin);
                        byte type = br.ReadByte();
                        UInt32 offset = br.ReadUInt32();
                        ms.Seek(pos + 5, SeekOrigin.Begin);
                        byte[] line = br.ReadBytes((int)offset);
                        try
                        {
                            ProcessLine(line);
                        }
                        catch (Exception Ex)
                        {
                            CheckThreadAbort(Ex);
                        };
                        pos += 5 + offset;
                    }
                }

                return true;

            }

            public override bool ProcessLine(byte[] line)
            {
                base.ProcessLine();
                Int16 ComponentNumber = 0;
                using (MemoryStream ms = new MemoryStream(line))
                {
                    // Use the memory stream in a binary reader.
                    using (BinaryReader br = new BinaryReader(ms))
                    {
                        ms.Seek(7, SeekOrigin.Begin);
                        ComponentNumber = br.ReadInt16();
                        ms.Seek(0x12, SeekOrigin.Begin);
                        UInt32 strlen = br.ReadUInt32();
                        char[] chrs = br.ReadChars((int)strlen);
                        string str = new string(chrs);
                        UInt32 DataLen = br.ReadUInt32();
                        byte[] data = br.ReadBytes((int)DataLen);
                        string Data = Encoding.UTF8.GetString(data, 0, data.Length);
                        ShapeBasedModel Model = new ShapeBasedModel(str);
                        if ((Model != null) && (ComponentNumber != -1))
                            ModulesL[ComponentNumber].ShapeBasedModels.Add(Model);
                        //ShapeBasedMods.Add(Model);
                    }
                }
                return true;
            }
        }
    }
}
