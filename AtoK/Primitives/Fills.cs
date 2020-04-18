using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ConvertToKicad
{
    public partial class ConvertPCBDoc
    {
        // class for fill objects
        class Fill : Object
        {
            private readonly double X1, Y1, X2, Y2, CX, CY;
            private readonly string layer;
            private readonly string net;

            private Fill()
            {
                X1 = 0;
                Y1 = 0;
                X2 = 0;
                Y2 = 0;
                CX = 0;
                CY = 0;
            }

            public Fill(double x1, double y1, double x2, double y2, string Layer, string Net)
            {
                X1 = x1;
                Y1 = y1;
                X2 = x2;
                Y2 = y2;
                CX = Math.Round(X2 - (X2 - X1) / 2, Precision); // centre X
                CY = Math.Round(Y2 - (Y2 - Y1) / 2, Precision); // centre Y
                layer = Layer;
                net = Net;
            }

            public override string ToString()
            {
                return $"  (gr_poly (pts (xy {X1} {-Y1}) (xy {X1} {-Y2}) (xy {X2} {-Y2}) (xy {X2} {-Y1})) (layer {layer}) (width 0))";
            }


            public override string ToString(double x, double y, double ModuleRotation)
            {
                // rotate the fill around the fill's centre point
                double angle = (ModuleRotation) * (Math.PI / 180); // Convert to radians
                // this is essentially doing a translate to 0,0 a rotate and a translate back
                double rotatedX1 = Math.Cos(angle) * (X1 - CX) - Math.Sin(angle) * (Y1 - CY) + CX;
                double rotatedY1 = Math.Sin(angle) * (X1 - CX) + Math.Cos(angle) * (Y1 - CY) + CY;
                double rotatedX2 = Math.Cos(angle) * (X2 - CX) - Math.Sin(angle) * (Y2 - CY) + CX;
                double rotatedY2 = Math.Sin(angle) * (X2 - CX) + Math.Cos(angle) * (Y2 - CY) + CY;

                // now make the points relative to the component origin
                Point2D p1 = new Point2D(rotatedX1 - x, rotatedY1 - y);
                Point2D p2 = new Point2D(rotatedX2 - x, rotatedY2 - y);

                // now rotate these points about the component centre
                p1.Rotate(ModuleRotation);
                p2.Rotate(ModuleRotation);

                // NB (width 0) = filled
                return $"    (fp_poly (pts (xy {p1.X} {-p1.Y}) (xy {p1.X} {-p2.Y}) (xy {p2.X} {-p2.Y}) (xy {p2.X} {-p1.Y})) (layer {layer}) (width 0))\n";
            }
        }

        // class for the fills document entry in the pcbdoc file
        class Fills : PcbDocEntry
        {
            Int16 Net;
            string NetName;
            double X1, Y1, X2, Y2;
            double Rotation;
            Int16 Component;
            bool InComponent;
            Layers Layer;
            int Locked;
            int Keepout;

            public Fills(string filename, string cmfilename, string record, Type type, int offset) : base(filename, cmfilename, record, type, offset)
            {
                //record length 47
                Binary_size = 47;
            }

            public override bool ProcessLine(byte[] record)
            {
                base.ProcessLine();
                using (MemoryStream ms = new MemoryStream(record))
                {
                    // Use the memory stream in a binary reader.
                    using (BinaryReader br = new BinaryReader(ms))
                    {
                        ms.Seek(0, SeekOrigin.Begin);
                        Layer = (Layers)br.ReadByte(); // line offset 0
                        Locked = br.ReadByte();        // line offset 1
                        Keepout = (int)br.ReadByte();  // line offset 2

                        ms.Seek(3 , SeekOrigin.Begin);
                        Net = br.ReadInt16();
                        Net += 1;
                        NetName = $"\"{NetsL[Net].Name}\"";
                        ms.Seek(7, SeekOrigin.Begin);
                        Component = br.ReadInt16();
                        InComponent = (Component != -1);
                        ms.Seek(13, SeekOrigin.Begin);
                        X1 = Math.Round(Bytes2mm(br.ReadBytes(4)) - originX, Precision);
                        ms.Seek(17, SeekOrigin.Begin);
                        Y1 = Math.Round(Bytes2mm(br.ReadBytes(4)) - originY, Precision);
                        ms.Seek(21, SeekOrigin.Begin);
                        X2 = Math.Round(Bytes2mm(br.ReadBytes(4)) - originX, Precision);
                        ms.Seek(25, SeekOrigin.Begin);
                        Y2 = Math.Round(Bytes2mm(br.ReadBytes(4)) - originY, Precision);
                        ms.Seek(29, SeekOrigin.Begin);
                        Rotation = br.ReadDouble();
                    }
                    if (Keepout == 2)
                    {
                        Point2D p1 = new Point2D(X1, Y1);
                        Point2D p2 = new Point2D(X2, Y2);
                        Point2D c = new Point2D(X1 + (X2 - X1) / 2, Y1 + (Y2 - Y1) / 2);
                        if (InComponent)
                        {
                            // need to factor in component's rotation
                            if (Component < ModulesL.Count)
                            {
                                try
                                {
                                    double rot = ModulesL[Component].Rotation;
                                    p1 = p1.Rotate(c, rot);
                                    p2 = p2.Rotate(c, rot);
                                }
                                catch (Exception Ex)
                                {
                                    CheckThreadAbort(Ex);
                                }
                            }
                        }
                        string layer = "";
                        if (Layer == Layers.Keepout_Layer)
                            layer = "*.Cu";
                        else
                            layer = Brd.GetLayer(Layer);

                        // generate a keepout
                        keepouts +=
$@"
    (zone(net 0)(net_name """")(layers {layer})(tstamp 0)(hatch edge 0.508)
      (connect_pads(clearance 0.508))
      (min_thickness 0.254)
      (keepout(tracks not_allowed)(vias not_allowed)(copperpour not_allowed))
      (fill(arc_segments 32)(thermal_gap 0.508)(thermal_bridge_width 0.508))
      (polygon
        (pts
          (xy {p1.X} {-p1.Y})(xy {p2.X} {-p1.Y})(xy {p2.X} {-p2.Y})(xy {p1.X} {-p2.Y})
         )
      )
    )
";

                    }
                    else if (!InComponent) // keepouts not allowed in components (yet)
                    {
                        fills += $"(gr_poly (pts (xy {X1} {-(Y1)}) (xy {X1} {-(Y2)}) (xy {X2} {-(Y2)}) (xy {X2} {-(Y1)})) (layer {Brd.GetLayer(Layer)}) (width 0))\n";
                    }
                    else
                    {
                        Fill Fill = new Fill(X1, Y1, X2, Y2, Brd.GetLayer(Layer), GetNetName(Net));
                        if (Component < ModulesL.Count && Component != -1)
                            ModulesL[Component].Fills.Add(Fill);
                    }
                    return true;
                }
            }


            public override bool ProcessBinaryFile(byte[] data)
            {
                StartTimer();
                if (Binary_size == 0)
                    return false;

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
                            // we are now pointing at a Fill record
                            byte[] record = br.ReadBytes((int)RecordLength);
                            p = ms.Position;
                            ProcessLine(record);
                        }
                    }
                }
                catch (Exception Ex)
                {
                    CheckThreadAbort(Ex);
                }
                return true;
            }
        }
    }
}
