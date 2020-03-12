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

            override public string ToString() //double X, double Y)
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
            // record size 208
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            unsafe struct ViaStruct
            {
                public byte Type;                   //  00 1 3 - type
                public UInt32 Offset;               //  01 4 203 - record size
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

            public Vias(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {
                Binary_size = 208;
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
                    ModulesL[Component].Pads.Add(Pad);
                }
                return true;
            }
        }
    }
}
