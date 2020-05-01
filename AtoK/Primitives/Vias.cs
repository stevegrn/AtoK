using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ConvertToKicad
{
    public partial class ConvertPCBDoc
    {
        // class for via objects
        class Via : Object
        {
            double X { get; set; }
            double Y { get; set; }
            double Size { get; set; }
            double Drill { get; set; }
            int Net { get; set; }
            Layers StartLayer { get; set; }
            Layers EndLayer { get; set; }

            private Via()
            {
                X = 0;
                Y = 0;
                Size = 0;
                Drill = 0;
                Net = 0;
            }

            public Via(double x, double y, double size, double drill, int net, Layers startlayer, Layers endlayer)
            {
                X = x;
                Y = y;
                Size = size;
                Drill = drill;
                Net = net;
                StartLayer = startlayer;
                EndLayer = endlayer;
            }

            override public string ToString()
            {
                string StartL = Brd.GetLayer(StartLayer);
                string EndL = Brd.GetLayer(EndLayer);
                string blind = ((StartL != "F.Cu") || (EndL != "B.Cu")) ? "blind" : "";
                return $"  (via {blind} (at {Math.Round(X, Precision)} {Math.Round(-Y, Precision)}) (size {Size}) (drill {Drill}) (layers {StartL} {EndL}) (net {Net}))\n";
            }
        }

        // class for the vias document entry in the pcbdoc file
        class Vias : PcbDocEntry
        {
            // record size 208 (under Winter 09)
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            unsafe struct ViaStruct
            {
                public byte U0;                     //  05 1 ???
                public byte U1;                     //  06 1 ???
                public byte U2;                     //  07 1 ???
                public Int16 net;                   //  08 2 net
                public Int16 U3;                    //  0A 2 ???
                public Int16 Component;             //  0C 2 component
                public Int16 U4;                    //  0E 2 ???
                public Int16 U5;                    //  10 2 ???
                public Int32 X;                     //  12 4 X
                public Int32 Y;                     //  16 4 Y
                public Int32 Width;                 //  1A 4 Width
                public Int32 Hole;                  //  1E 4 Hole
                public byte StartLayer;             //  22 1 start layer
                public byte EndLayer;               //  23 1 end layer
                public fixed byte U7[0x50 - 0x24];  //  24 44 ???
                public fixed Int32 LayerSizes[32];  //  50 128 pad size on layers top, mid1,...mid30, bottom 
            }

            public Vias(string filename, string cmfilename, string record, Type type, int offset) : base(filename, cmfilename, record, type, offset)
            {
                Binary_size = 208;
            }

            public override bool ProcessBinaryFile(byte[] data)
            {
                bool GenerateTxtFile = true;
                FileStream TextFile = null;
                StartTimer();
                if (Binary_size == 0)
                    return false;

                if (GenerateTxtFile)
                {
                    if (ExtractFiles)
                        TextFile = File.Open("Data.txt", FileMode.OpenOrCreate);
                }
                try
                {
                    using (MemoryStream ms = new MemoryStream(data))
                    {
                        uint size = (uint)ms.Length;
                        long p = 0;

                        BinaryReader br = new BinaryReader(ms, System.Text.Encoding.UTF8);
                        // find the headers and process
                        while (p < size)
                        {
                            ms.Seek(p + 1, SeekOrigin.Begin);
                            UInt32 RecordLength = br.ReadUInt32();
                            // we are now pointing at a via record
                            byte[] record = br.ReadBytes((int)RecordLength);
                            p = ms.Position;
                            ProcessLine(record);
                        }
                    }
                    TextFile.Close();
                }
                catch (Exception Ex)
                {
                    CheckThreadAbort(Ex);
                }
                return true;
        }

        public override bool ProcessLine(byte[] line)
            {
                Layers StartLayer, EndLayer;
                double X, Y, Width, HoleSize;
                double RRatio = 0;
                Int16 Component;
                Int16 Net;
                ViaStruct via = ByteArrayToStructure<ViaStruct>(line);
                base.ProcessLine();

                StartLayer = (Layers)via.StartLayer;
                EndLayer = (Layers)via.EndLayer;
                Net = via.net;
                Net++;
                Component = via.Component;
                X = Math.Round(ToMM(via.X) - originX, Precision);
                Y = Math.Round(ToMM(via.Y) - originY, Precision);
                Width = ToMM(via.Width);
                HoleSize = ToMM(via.Hole);
                bool InComponent = Component != -1;

                if (!InComponent)
                {
                    Via Via = new Via(X, Y, Width, HoleSize, Net, StartLayer, EndLayer);
                    ViasL.Add(Via);
                }
                else
                {
                    // can't have vias in components in Kicad (yet) so add as a pad
                    Pad Pad = new Pad("0", "thru_hole", "circle", X, Y, 0, Width, Width, HoleSize, "*.Cu", Net, RRatio);
                    if (Component > 0 && Component < ModulesL.Count)
                        ModulesL[Component].Pads.Add(Pad);
                    else
                        OutputError($"Invalid component {Component}");
                }
                return true;
            }
        }
    }
}
