using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ConvertToKicad
{
    public partial class ConvertPCBDoc
    {
        // class for region objects
        class Region : Object
        {
            List<Point> Points;
            int Net_no { get; set; }
            string Net_name { get; set; }
            string Layer { get; set; }
            bool InComponent { get; set; }
            public bool Keepout { get; set; }

            public void AddPoint(double X, double Y)
            {
                Point Point = new Point(X, Y);
                Points.Add(Point);
            }

            private Region()
            {

            }

            public Region(string layer, int net, bool keepout)
            {
                Layer = layer;
                Net_no = net;
                Net_name = GetNetName(Net_no);
                Points = new List<Point>();
                Keepout = keepout;
            }

            public override string ToString()
            {
                string ret = "";
                string connectstyle = "";

                double clearance = GetRuleValue("Clearance", "PolygonClearance");
                if (Layer.Substring(0, 2) == "In")
                {
                    // this is an inner layer so use plane clearance
                    clearance = GetRuleValue("PlaneClearance", "PlaneClearance");
                }

                //        if (!Keepout)
                //            Layer = "Dwgs.User";
                if (Layer != "Edge.Cuts")
                {
                    ret = $"  (zone (net {Net_no}) (net_name {Net_name}) (layer {Layer}) (tstamp 0) (hatch edge 0.508)";
                    ret += $"    (priority 100)\n";
                    ret += $"    (connect_pads {connectstyle} (clearance {clearance}))\n"; // TODO sort out these numbers properly
                    ret += $"    (min_thickness 0.2)\n";
                    if (Keepout)
                        ret += "(keepout(copperpour not_allowed))\n";
                    string fill = (Layer == "Edge.Cuts") ? "no" : "yes";
                    ret += $"    (fill {fill} (arc_segments 16) (thermal_gap 0.2) (thermal_bridge_width 0.3))\n";
                    var i = 0;
                    ret += "    (polygon (pts\n        ";
                    foreach (var Point in Points)
                    {
                        i++;
                        if ((i % 5) == 0)
                            ret += "\n        ";
                        ret += Point.ToString();
                    }
                    ret += "\n      )\n    )\n  )\n";
                }
                else
                {
                    Point Start = new Point(0, 0);
                    Start = Points[0];
                    int i;
                    for (i = 0; i < Points.Count - 1; i++)
                    {
                        ret += $"  (gr_line (start {Points[i].X} {-Points[i].Y}) (end {Points[i + 1].X} {-Points[i + 1].Y}) (layer Edge.Cuts) (width 0.1))\n";
                    }
                    ret += $"  (gr_line (start {Points[i].X} {-Points[i].Y}) (end {Start.X} {-Start.Y}) (layer Edge.Cuts) (width 0.1))\n";
                }
                return ret;
            }

            // inside component region
            // presently this is not allowed (V5.1.2)
            public override string ToString(double x, double y, double ModuleRotation)
            {
                string ret = "";

                double clearance = GetRuleValue("Clearance", "PolygonClearance");
                if (Layer.Substring(0, 2) == "In")
                {
                    // this is an inner layer so use plane clearance
                    clearance = GetRuleValue("PlaneClearance", "PlaneClearance");
                }

                /*
                    string connectstyle = ""; // TODO get connect style from rules
                    ret = $"  (zone (net {net_no}) (net_name {net_name}) (layer {Layer}) (tstamp 0) (hatch edge 0.508)";
                    ret += $"    (priority 100)\n";
                    ret += $"    (connect_pads {connectstyle} (clearance {clearance}))\n"; // TODO sort out these numbers properly
                    ret += $"    (min_thickness 0.2)\n";
                    ret += "    (fill yes (arc_segments 16) (thermal_gap 0.2) (thermal_bridge_width 0.3))\n";
                 */
                var i = 0;
                ret += "    (fp_poly (pts\n        ";
                foreach (var Point in Points)
                {
                    i++;
                    if ((i % 5) == 0)
                        ret += "\n        ";
                    ret += Point.ToString(x, y, ModuleRotation);
                }
                //                ret += "\n      )\n    )\n  )\n";
                ret += $" ) ( layer {Brd.GetLayer(Layer)}) (width 0)\n    )\n";

                return ret;
            }
        }

        // class for the regions document entry in the pcbdoc file
        class Regions : PcbDocEntry
        {
            class Point
            {
                private readonly double X;
                private readonly double Y;

                Point()
                {
                }

                public Point(double x, double y)
                {
                    X = Math.Round(ToMM(x) - originX, Precision);
                    Y = Math.Round(ToMM(y) - originY, Precision);
                }
            }

            private readonly List<Point> Points;

            // variable length records
            public Regions(string filename, string cmfilename, string record, Type type, int offset) : base(filename, cmfilename, record, type, offset)
            {
                Binary_size = 1;
                Points = new List<Point>();
            }

            public override bool ProcessLine(byte[] record)
            {
                Layers Layer;
                Int16 Component;
                bool InComponent;
                base.ProcessLine();

                using (MemoryStream ms = new MemoryStream(record))
                {
                    // Use the memory stream in a binary reader.
                    using (BinaryReader br = new BinaryReader(ms))
                    {
                        try
                        {
                            ms.Seek(0, SeekOrigin.Begin);
                            Layer = (Layers)br.ReadByte();
                            ms.Seek(2, SeekOrigin.Begin);
                            bool Keepout = br.ReadByte() != 0;
                            ms.Seek(3, SeekOrigin.Begin);
                            int net = (int)br.ReadInt16();
                            ms.Seek(7, SeekOrigin.Begin);
                            Component = br.ReadInt16();
                            InComponent = Component != -1;
                            ms.Seek(0x12, SeekOrigin.Begin);
                            int strlen = br.ReadInt32();
                            ms.Seek(0x16, SeekOrigin.Begin);
                            byte[] bytes = br.ReadBytes(strlen);
                            string str = ConvertToString(bytes);
                            string[] words = str.Split('|');
                            ms.Seek(0x16 + strlen, SeekOrigin.Begin);
                            Int32 DataLen = br.ReadInt32();
                            string l = Brd.GetLayer((Layers)Layer);
                            if (GetString(str, "ISBOARDCUTOUT=") == "TRUE")
                                l = "Edge.Cuts";
                            Region r = new Region(l, net, Keepout);

                            while (DataLen-- > 0)
                            {
                                double X = Math.Round(ToMM(br.ReadDouble()) - originX, Precision);
                                double Y = Math.Round(ToMM(br.ReadDouble()) - originY, Precision);
                                if (X > 10000 || Y > 10000 || Y<0)  // TODO sort out this frig these come from second Regions append
                                    return false;
                                r.AddPoint(X, Y);
                            }
                            r.Keepout = Keepout;

                            if (!InComponent)
                            {
                                RegionsL.Add(r);
                            }
                            else
                            {
                                if (!Keepout)
                                    ModulesL[Component].Regions.Add(r);
                                else
                                    // until keepouts are allowed in components
                                    // just add as a board region
                                    RegionsL.Add(r);
                            }
                        }
                        catch (Exception Ex)
                        {
                            CheckThreadAbort(Ex);
                        }
                    }
                }

                return true;
            }

            public override bool ProcessBinaryFile(byte[] data)
            {
                StartTimer();
                if (Binary_size == 0)
                    return false;

                List<UInt32> Headers = new List<UInt32>();

                try
                {
                    using (MemoryStream ms = new MemoryStream(data))
                    {
                        long p = 0;
                        long size = ms.Length;
                        BinaryReader br = new BinaryReader(ms, System.Text.Encoding.UTF8);
                        // find the headers
                        while (p < size)
                        {
                            ms.Seek(p + 1, SeekOrigin.Begin);
                            UInt32 RecordLength = br.ReadUInt32();
                            // we are now pointing to the region record
                            //
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
