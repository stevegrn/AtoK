using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ConvertToKicad
{
    public partial class ConvertPCBDoc
    {
        // class for track/arc objects which are part of the board outline
        class BoundaryObject : Object
        {
            enum Types { Line = 0, Arc = 2};

            public double X1 { get; set; }
            public double Y1 { get; set; }
            public double X2 { get; set; }
            public double Y2 { get; set; }
            public double Angle { get; set; } // for arcs
            private int Type;

            private BoundaryObject()
            {
                X1 = 0;
                Y1 = 0;
                X2 = 0;
                Y2 = 0;
                Type = (int)Types.Line;
            }

            public BoundaryObject(double x1, double y1, double x2, double y2)
            {
                X1 = x1;
                Y1 = y1;
                X2 = x2;
                Y2 = y2;
                Angle = 0;
                Type = (int)Types.Line;
            }

            public BoundaryObject(double x1, double y1, double x2, double y2, double angle)
            {
                X1 = x1;
                Y1 = y1;
                X2 = x2;
                Y2 = y2;
                Angle = angle;
                Type = (int)Types.Arc;
            }

            public override string ToString()
            {
                if (Type == (int)Types.Line)
                    return $"  (gr_line (start {X1} {-Y1}) (end {X2} {-Y2}) (layer Edge.Cuts) (width 0.05))\n";
                else
                    return $"  (gr_arc (start {X1} {-Y1}) (end {X2} {-Y2}) (angle {Angle}) (layer Edge.Cuts) (width {0.05}))\n";
                ;
            }
        }

        // class for the board document in the pcbdoc file
        class Board : PcbDocEntry
        {
            public List<BoundaryObject> BoundaryObjects;

            public string OutputBoardOutline()
            {
                string outline = "# Board Outline\n";
                foreach (var Obj in BoundaryObjects)
                    outline += Obj.ToString();
                outline += "# End Board outline\n";
                return outline;
            }

            public class Layer
            {
                public string Name { get; set; }
                public int Prev { get; set; }
                public int Next { get; set; }
                public bool MechEnabled { get; set; }
                public double CopperThickness { get; set; }
                public int DielectricType { get; set; }
                public double DielectricConstant { get; set; }
                public double DielectricHeight { get; set; }
                public string DielectricMaterial { get; set; }
                public int Number { get; set; }
                public string PcbNewLayer { get; set; }
                public string AltiumName { get; set; }
                public Layer(string line, int number)
                {
                    Name = GetString(line, $"LAYER{number}NAME=");
                    Prev = Convert.ToInt16(GetString(line, $"LAYER{number}PREV="));
                    Next = Convert.ToInt16(GetString(line, $"LAYER{number}NEXT="));
                    MechEnabled = GetString(line, $"LAYER{number}MECHENABLED=") == "TRUE";
                    CopperThickness = GetNumberInMM(GetString(line, $"LAYER{number}COPTHICK="));
                    DielectricType = Convert.ToInt16(GetString(line, $"LAYER{number}DIELTYPE="));
                    DielectricConstant = Convert.ToDouble(GetString(line, $"LAYER{number}DIELCONST="));
                    DielectricHeight = GetNumberInMM(GetString(line, $"LAYER{number}DIELHEIGHT="));
                    DielectricMaterial = GetString(line, $"LAYER{number}DIELMATERIAL=");
                    Number = number;
                    AltiumName = LayerNames[Number];
                }

                public void AssignPcbNewLayer(int Number, int total)
                {
                    if (Number == 0) PcbNewLayer = "F.Cu";
                    else if (Number == total - 1) PcbNewLayer = "B.Cu";
                    else PcbNewLayer = $"In{Number}.Cu";
                }

            }

            public double SheetWidth { get; set; }
            public double SheetHeight { get; set; }
            public double OriginX { get; set; }
            public double OriginY { get; set; }
            public bool DesignatorDisplayMode { get; set; }
            public int InnerLayerCount { get; set; }
            public List<Layer> LayersL;
            public List<Layer> OrderedLayers;

            public Board() : base()
            {
            }

            public Board(string filename, string cmfilename, string record, Type type, int off) : base(filename, cmfilename, record, type, off)
            {
                LayersL = new List<Layer>();
                OrderedLayers = new List<Layer>();
                BoundaryObjects = new List<BoundaryObject>();
            }

            public void BoardAddLine(double x1, double y1, double x2, double y2)
            {
                if (Length(x1, y1, x2, y2) <= 0.01)
                {
                    OutputError($"Rejected zero length line in boundary at {x1} {y1} {x2} {y2}");
                    return;
                }
                BoundaryObject Line = new BoundaryObject(x1, y1, x2, y2);
                BoundaryObjects.Add(Line);
            }

            public bool CheckExistingLine(double x1, double y1, double x2, double y2)
            {
                foreach (var Line in BoundaryObjects)
                {
                    if (Line.X1 == x1 || Line.Y1 == y1 || Line.X2 == x2 || Line.Y2 == y2)
                        return true;
                }
                return false;
            }

            private double ArcLength(double X1, double Y1, double X2, double Y2, double Angle)
            {
                return 2 * Math.PI * Length(X1,Y1,X2,Y2) * (Math.Abs(Angle) / 360);
            }

            public void BoardAddArc(double X1, double Y1, double X2, double Y2, double Angle)
            {
                if (ArcLength(X1, Y1, X2, Y2, Angle)<=0.01)
                {
                    OutputError($"Rejected zero length arc in boundary {X1} {Y1} {X2} {Y2} {Angle}");
                    return;
                }
                BoundaryObject Arc = new BoundaryObject(X1, Y1, X2, Y2, Angle);
                BoundaryObjects.Add(Arc);
            }

            public bool CheckExistingArc(double x1, double y1, double x2, double y2, double angle)
            {
                foreach (var Arc in BoundaryObjects)
                {
                    if (Arc.Angle == angle && Arc.X1 == x1 && Arc.Y1 == y1 && Arc.X2 == x2 && Arc.Y2 == y2)
                        return true;
                }
                return false;
            }

            public override bool ProcessLine(string line)
            {
                base.ProcessLine();
                try
                {
                    InnerLayerCount = 0;
                    string ORIGINX = GetString(line, "ORIGINX=");
                    string ORIGINY = GetString(line, "ORIGINX=");
                    if (ORIGINX != "")
                        OriginX = originX = GetCoordinateX(ORIGINX);
                    if (ORIGINY != "")
                        OriginY = originY = GetCoordinateY(GetString(line, "ORIGINY="));
                    OutputString($"ORIGINX={OriginX} ORIGINY={OriginY}");
                    if (ORIGINX != "" && ORIGINY != "")
                    {
                        List<string> strings = new List<string>();
                        // this is the first line in the file and contains the board outline details
                        int count = 0;
                        string search;
                        int position = 0;
                        bool done = false;
                        do
                        {
                            search = $"KIND{count}=";
                            int start = line.IndexOf(search, position);
                            search = $"KIND{count + 1}=";
                            int end = line.IndexOf(search, position);
                            if (end == -1)
                            {
                                end = line.IndexOf("SHELVED", position);
                                done = true;
                            }
                            position = end;
                            string found = line.Substring(start, end - start);
                            strings.Add(found);
                            count++;
                        } while (!done);

                        count = 0;
                        double x, y, cx, cy, sa, ea, r, x0 = 0, y0 = 0, nx, ny;
                        foreach (var found in strings)
                        {
                            search = $"KIND{count}=";
                            string Kind = GetString(found, search);
                            x = GetCoordinateX(GetString(found, $"VX{count}="));
                            y = GetCoordinateY(GetString(found, $"VY{count}="));
                            if (count == 0)
                            {
                                // record first coordinate
                                x0 = x;
                                y0 = y;
                            }
                            if (count < strings.Count - 1)
                            {
                                nx = GetCoordinateX(GetString(strings[count + 1], $"VX{count + 1}="));
                                ny = GetCoordinateY(GetString(strings[count + 1], $"VY{count + 1}="));
                            }
                            else
                            {
                                nx = x0;
                                ny = y0;
                            }
                            cx = GetCoordinateX(GetString(found, $"CX{count}="));
                            cy = GetCoordinateY(GetString(found, $"CY{count}="));
                            sa = Convert.ToDouble(GetString(found, $"SA{count}="));
                            ea = Convert.ToDouble(GetString(found, $"EA{count}="));
                            r = GetNumberInMM(GetString(found, $"R{count}="));
                            count++;
                            if (Kind == "0")
                            {
                                BoardAddLine(x, y, nx, ny);
                            }
                            else
                            {
                                double X1 = cx;
                                double Y1 = cy;


                                if (ea < sa)
                                    ea += 360;

                                double Angle = Math.Round(-(ea - sa), Precision);
                                double X = Math.Round(cx + r * Math.Cos(sa * Math.PI / 180), Precision);
                                double Y = Math.Round(cy + r * Math.Sin(sa * Math.PI / 180), Precision);
                                BoardAddArc(X1, Y1, X, Y, Angle);
                            }
                        }

                    }

                    if (LayersL.Count == 0)
                    {
                        try
                        {
                            for (var i = 1; i < 83; i++)
                            {
                                Layer Layer = new Layer(line, i);
                                //OutputError($"Found layer - {Layer.AltiumName} name={Layer.Name} number={Layer.Number}");
                                if (Layer.Prev != 0 || Layer.Next != 0)
                                    // only add layers that are in the layer stack
                                    LayersL.Add(Layer);
                            }
                        }
                        catch (Exception Ex)
                        {
                            CheckThreadAbort(Ex);
                        }

                        // now sort the list in terms of the prev,next parameters
                        // first in list is the layer with Prev=0

                        int Next = 0;

                        foreach (var Layer in LayersL)
                        {
                            if (Layer.Prev == 0)
                            {
                                OrderedLayers.Add(Layer);
                                Next = Layer.Next;
                                break;
                            }
                        }

                        bool End = false;
                        do
                        {
                            foreach (var Layer in LayersL)
                            {
                                if (Layer.Number == Next)
                                {
                                    OrderedLayers.Add(Layer);
                                    Next = Layer.Next;
                                    if (Layer.Next == 0)
                                        End = true;
                                    break;
                                }
                            }
                        } while (!End);

                        for (var i = 0; i < OrderedLayers.Count; i++)
                        {
                            OrderedLayers[i].AssignPcbNewLayer(i, OrderedLayers.Count);
                        }

                        InnerLayerCount = OrderedLayers.Count;

                        SheetWidth = GetNumberInMM(GetString(line, "SHEETWIDTH="));
                        SheetHeight = GetNumberInMM(GetString(line, "SHEETHEIGHT="));
                        DesignatorDisplayMode = GetString(line, "DESIGNATORDISPLAYMODE=") == "1";
                    }
                }
                catch (Exception Ex)
                {
                    CheckThreadAbort(Ex);
                }

                return true;
            }

            // output the layers as a string
            public override string ToString()
            {
                string Layers = "";
                int i = 0;
                if(OrderedLayers.Count % 2 == 1)
                {
                    // odd number of layers not allowed in PCBNew so insert one
                    OrderedLayers.Insert(OrderedLayers.Count - 1, OrderedLayers[InnerLayerCount - 2]);
                    OrderedLayers[OrderedLayers.Count - 2].PcbNewLayer = $"In{OrderedLayers.Count - 2}.Cu";
                }
                foreach (Layer Layer in OrderedLayers)
                {
                    string Type = "";
                    Type = (Layer.AltiumName.Substring(0, Precision) == "Int") ? "power" : "signal";
                    if (Layer.PcbNewLayer == "B.Cu")
                        i = 31;
                    Layers += $"    ({i++} {Layer.PcbNewLayer} {Type})\n";
                }
                return Layers;
            }

            // binary based get layer
            public string GetLayer(Layers AltiumLayer)
            {
                // perhaps should get layer mapping from a file...not everybody uses the mech layers the same
                // TODO it's not as simple as this as the layer stackup determines which Inx.Cu is mapped to which Altium layer
                // this bit takes care of any copper layers
                foreach (var Layer in OrderedLayers)
                {
                    if ((Layers)Layer.Number == AltiumLayer)
                    {
                        return Layer.PcbNewLayer;
                    }
                }

                switch (AltiumLayer)
                {
                    /*
                    case Layers.top_layer: return "F.Cu";*/
                    case Layers.Multi_Layer: return "*.Cu"; // *.Mask";
/*                    case Layers.bottom_layer: return "B.Cu";
                    case Layers.plane_1: return "In1.Cu";
                    case Layers.plane_2: return "In2.Cu";
                    case Layers.plane_3: return "In3.Cu";
                    case Layers.plane_4: return "In4.Cu";
                    case Layers.plane_5: return "In5.Cu";
                    case Layers.plane_6: return "In6.Cu";
                    case Layers.plane_7: return "In7.Cu";
                    case Layers.plane_8: return "In8.Cu";
                    case Layers.plane_9: return "In9.Cu";
                    case Layers.plane_10: return "In10.Cu";
                    case Layers.plane_11: return "In11.Cu";
                    case Layers.plane_12: return "In12.Cu";
                    case Layers.plane_13: return "In13.Cu";
                    case Layers.plane_14: return "In14.Cu";
                    case Layers.plane_15: return "In15.Cu";
                    case Layers.plane_16: return "In16.Cu";
                                                            */
                    case Layers.Top_Overlay: return "F.SilkS";
                    case Layers.Bottom_Overlay: return "B.SilkS";
                    case Layers.Keepout_Layer: return "Margin";
                    case Layers.Mech_1: return "Dwgs.User"; //"Edge.Cuts";
                    case Layers.Mech_13: return "Dwgs.User";
                    case Layers.Mech_15: return "F.CrtYd";
                    case Layers.Mech_16: return "B.CrtYd";
                    case Layers.Mech_11: return "Eco1.User";
                    case Layers.Top_Solder: return "F.Mask";
                    case Layers.Bottom_Solder: return "B.Mask";
                    case Layers.Mech_9: return "Dwgs.User";
                    case Layers.Mech_10: return "Dwgs.User";
                    case Layers.Bottom_Paste: return "B.Paste";
                    case Layers.Top_Paste: return "F.Paste";
                    case Layers.Drill_Drawing: return "Dwgs.User";
                    case Layers.Drill_Guide: return "Dwgs.User";
                    default: return "Dwgs.User";
                }
            }

            // string based get layer i.e. for ASCII data.dat files
            public string GetLayer(string AltiumLayer)
            {
                string layer = "";

                foreach (var Layer in OrderedLayers)
                {
                    if (Layer.AltiumName == AltiumLayer)
                    {
                        return Layer.PcbNewLayer;
                    }
                }

                switch (AltiumLayer)
                {

                    /*                  case "TOP": layer += "F.Cu"; break;
                                        case "BOTTOM": layer += "B.Cu"; break;
                                        case "PLANE1": layer += "In1.Cu"; break;
                                        case "PLANE2": layer += "In2.Cu"; break;
                    */
                    case "MULTILAYER":    layer += "F.Cu F.Mask B.Cu B.Mask"; break;
                    case "TOPOVERLAY":    layer += "F.SilkS"; break;
                    case "BOTTOMOVERLAY": layer += "B.SilkS"; break;
                    case "KEEPOUT":       layer += "Margin"; break;
                    case "MECHANICAL1":   layer += "Edge.Cuts"; break;
                    case "MECHANICAL2":   layer += "Dwgs.User"; break;
                    case "MECHANICAL3":   layer += "Dwgs.User"; break;
                    case "MECHANICAL4":   layer += "Dwgs.User"; break;
                    case "MECHANICAL5":   layer += "Dwgs.User"; break;
                    case "MECHANICAL6":   layer += "Dwgs.User"; break;
                    case "MECHANICAL7":   layer += "Dwgs.User"; break;
                    case "MECHANICAL8":   layer += "Dwgs.User"; break;
                    case "MECHANICAL9":   layer += "Dwgs.User"; break;
                    case "MECHANICAL10":  layer += "Dwgs.User"; break;
                    case "MECHANICAL11":  layer += "Eco1.User"; break;
                    case "MECHANICAL12":  layer += "Dwgs.User"; break;
                    case "MECHANICAL13":  return "Dwgs.User";
                    case "MECHANICAL14": layer += "Dwgs.User"; break;
                    case "MECHANICAL15": layer += "F.CrtYd"; break;
                    case "MECHANICAL16": layer += "B.CrtYd"; break;
                    case "TOPSOLDER":    layer += "F.Mask"; break;
                    case "BOTTOMSOLDER": layer += "B.Mask"; break;
                    case "BOTTOMPASTE":  layer += "B.Paste"; break;
                    case "TOPPASTE":     layer += "F.Paste"; break;
                    case "DRILLDRAWING": layer += "Dwgs.User"; break;
                    case "DRILLGUIDE":   layer += "Dwgs.User"; break;
                    default: return AltiumLayer;
                }
                return layer;
            }

            public bool IsCopperLayer(Layers AltiumLayer)
            {
                if (AltiumLayer == Layers.Multi_Layer)
                    return true;
                foreach (var Layer in OrderedLayers)
                {
                    if ((Layers)Layer.Number == AltiumLayer)
                    {
                        return true;
                    }
                }

                return false;
            }
            /*
            // get the number of a copper layer
            public int GetAltiumCopperLayerNumber(string Layer)
            {
                switch (Layer)
                {
                    case "Top Layer": return 1;
                    case "MidLayer1": return 2;
                    case "MidLayer2": return 3;
                    case "MidLayer3": return 4;
                    case "MidLayer4": return 5;
                    case "MidLayer5": return 6;
                    case "MidLayer6": return 7;
                    case "MidLayer7": return 8;
                    case "MidLayer8": return 9;
                    case "MidLayer9": return 10;
                    case "MidLayer10": return 11;
                    case "MidLayer11": return 12;
                    case "MidLayer12": return 13;
                    case "MidLayer13": return 14;
                    case "MidLayer14": return 15;
                    case "MidLayer15": return 16;
                    case "MidLayer16": return 17;
                    case "MidLayer17": return 18;
                    case "MidLayer18": return 19;
                    case "MidLayer19": return 20;
                    case "MidLayer20": return 21;
                    case "MidLayer21": return 22;
                    case "MidLayer22": return 23;
                    case "MidLayer23": return 24;
                    case "MidLayer24": return 25;
                    case "MidLayer25": return 26;
                    case "MidLayer26": return 27;
                    case "MidLayer27": return 28;
                    case "MidLayer28": return 29;
                    case "MidLayer29": return 30;
                    case "MidLayer30": return 31;
                    case "Bottom Layer": return 32;
                    case "InternalPlane1": return 39;
                    case "InternalPlane2": return 40;
                    case "InternalPlane3": return 41;
                    case "InternalPlane4": return 42;
                    case "InternalPlane5": return 43;
                    case "InternalPlane6": return 44;
                    case "InternalPlane7": return 45;
                    case "InternalPlane8": return 46;
                    case "InternalPlane9": return 47;
                    case "InternalPlane10": return 48;
                    case "InternalPlane11": return 49;
                    case "InternalPlane12": return 50;
                    case "InternalPlane13": return 51;
                    case "InternalPlane14": return 52;
                    case "InternalPlane15": return 53;
                    case "InternalPlane16": return 54;
                }
                return 0;
            }
            */
            // used for non-net tracks on inner copper layers
            // to eliminate them as PCBNEW doesn't do negative planes
            public bool OnInnerLayer(Layers Layer)
            {
                for (var i = 1; i < OrderedLayers.Count; i++)
                {
                    if ((Layers)OrderedLayers[i].Number == Layer)
                        return true;
                }
                return false;
            }

        }
    }
}
