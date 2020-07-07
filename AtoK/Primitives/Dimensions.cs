using System;
using System.Text;
using System.Collections.Generic;

namespace ConvertToKicad
{
    public partial class ConvertPCBDoc
    {
        // class for dimension objects (not all dimension types catered for)
        class Dimension : PCBObject
        {
            private readonly string layer = "";
            private string text = "";
            private readonly double X1 = 0, Y1 = 0, X2 = 0, Y2 = 0, LX = 0, LY = 0, HX = 0, HY = 0, REFERENCE0POINTX = 0, REFERENCE0POINTY = 0,
                REFERENCE1POINTX = 0, REFERENCE1POINTY = 0, ARROWSIZE = 0, ARROWLINEWIDTH = 0, ARROWLENGTH = 0, TEXTX = 0, TEXTY = 0, TEXT1X = 0, TEXT1Y = 0,
                LINEWIDTH = 0, TEXTHEIGHT = 0, TEXTWIDTH = 0, ANGLE = 0;
            private readonly int TEXTPRECISION;
            private readonly Int16 DIMENSIONKIND;
            private readonly char[] charsToTrim = { 'm', 'i', 'l' };
            private string DimensionUnit = "";

            double length;

            private string GetString(string line, string s)
            {
                int index;
                int start, end;
                int length;
                index = line.IndexOf(s);
                if (index == -1)
                    return "";
                start = index + s.Length;
                end = line.IndexOf('|', start);
                if (end == -1)
                    length = line.Length - start;
                else
                    length = end - start;

                string param = line.Substring(start, length);

                return param;
            }

            private Dimension()
            {

            }

            public Dimension(string line)
            {
                string param;
                if ((param = GetString(line, "|X1=").Trim(charsToTrim)) != "")
                {
                    X1 = GetCoordinateX(param);
                }
                if ((param = GetString(line, "|Y1=").Trim(charsToTrim)) != "")
                {
                    Y1 = GetCoordinateY(param);
                }
                if ((param = GetString(line, "|X2=").Trim(charsToTrim)) != "")
                {
                    X2 = GetCoordinateX(param);
                }
                if ((param = GetString(line, "|Y2=").Trim(charsToTrim)) != "")
                {
                    Y2 = GetCoordinateY(param);
                }

                if ((param = GetString(line, "|DIMENSIONKIND=")) != "")
                {
                    DIMENSIONKIND = Convert.ToInt16(param);
                }
                if ((param = GetString(line, "|DIMENSIONLAYER=")) != "")
                {
                    layer = Brd.GetLayer(param);
                }
                if ((param = GetString(line, "|LX=").Trim(charsToTrim)) != "")
                {
                    LX = GetCoordinateX(param);
                }
                if ((param = GetString(line, "|LY=").Trim(charsToTrim)) != "")
                {
                    LY = GetCoordinateY(param);
                }
                if ((param = GetString(line, "|HX=").Trim(charsToTrim)) != "")
                {
                    HX = GetCoordinateX(param);
                }
                if ((param = GetString(line, "|HY=").Trim(charsToTrim)) != "")
                {
                    HY = GetCoordinateY(param);
                }
                if ((param = GetString(line, "|REFERENCE0POINTX=").Trim(charsToTrim)) != "")
                {
                    REFERENCE0POINTX = GetCoordinateX(param);
                }
                if ((param = GetString(line, "|REFERENCE0POINTY=").Trim(charsToTrim)) != "")
                {
                    REFERENCE0POINTY = GetCoordinateY(param);
                }
                if ((param = GetString(line, "|REFERENCE1POINTX=").Trim(charsToTrim)) != "")
                {
                    REFERENCE1POINTX = GetCoordinateX(param);
                }
                if ((param = GetString(line, "|REFERENCE1POINTY=").Trim(charsToTrim)) != "")
                {
                    REFERENCE1POINTY = GetCoordinateY(param);
                }
                if ((param = GetString(line, "|ANGLE=").Trim(charsToTrim)) != "")
                {
                    ANGLE = Math.Round(Convert.ToDouble(param), Precision);
                    if (ANGLE == 180 || ANGLE == 360)
                        ANGLE = 0;
                    if (ANGLE == 270)
                        ANGLE = 90;
                }
                if ((param = GetString(line, "|ARROWSIZE=").Trim(charsToTrim)) != "")
                {
                    ARROWSIZE = GetNumberInMM(param);
                }
                if ((param = GetString(line, "|ARROWLINEWIDTH=").Trim(charsToTrim)) != "")
                {
                    ARROWLINEWIDTH = GetNumberInMM(param);
                }
                if ((param = GetString(line, "|ARROWLENGTH=").Trim(charsToTrim)) != "")
                {
                    ARROWLENGTH = GetNumberInMM(param);
                }
                if ((param = GetString(line, "|LINEWIDTH=").Trim(charsToTrim)) != "")
                {
                    LINEWIDTH = GetNumberInMM(param);
                }
                if ((param = GetString(line, "|TEXTHEIGHT=").Trim(charsToTrim)) != "")
                {
                    TEXTHEIGHT = GetNumberInMM(param);
                }
                if ((param = GetString(line, "|TEXTWIDTH=").Trim(charsToTrim)) != "")
                {
                    TEXTWIDTH = GetNumberInMM(param);
                }
                if ((param = GetString(line, "|TEXTHEIGHT=").Trim(charsToTrim)) != "")
                {
                    TEXTHEIGHT = GetNumberInMM(param);
                }
                if ((param = GetString(line, "|TEXTX=").Trim(charsToTrim)) != "")
                {
                    TEXTX = GetCoordinateX(param);
                }
                if ((param = GetString(line, "|TEXTY=").Trim(charsToTrim)) != "")
                {
                    TEXTY = GetCoordinateY(param);
                }
                if ((param = GetString(line, "|TEXT1X=").Trim(charsToTrim)) != "")
                {
                    TEXT1X = GetCoordinateX(param);
                }
                if ((param = GetString(line, "|TEXT1Y=").Trim(charsToTrim)) != "")
                {
                    TEXT1Y = GetCoordinateY(param);
                }
                if ((param = GetString(line, "|TEXTPRECISION=").Trim(charsToTrim)) != "")
                {
                    TEXTPRECISION = Convert.ToInt32(param);
                }
                if ((param = GetString(line, "|TEXTDIMENSIONUNIT=").Trim(charsToTrim)) != "")
                {
                    DimensionUnit = param;
                }
            }

            public override string ToString()
            {
                if (DIMENSIONKIND != 1) // TODO fix this - filter out radial dimensions
                {
                    OutputError($"Unsupported Dimension kind ({DIMENSIONKIND}) at {X1},{Y1}");
                    return "";
                }
                // translate to fit in worksheet
                Point2D R0 = new Point2D(REFERENCE0POINTX, REFERENCE0POINTY);
                Point2D R1 = new Point2D(REFERENCE1POINTX, REFERENCE1POINTY);
                Point2D centre = new Point2D(X1, Y1);

                // rotate the two reference points to make horizontal dimension
                R0 = R0.Rotate(centre, -ANGLE);
                R1 = R1.Rotate(centre, -ANGLE);
                Point2D end = new Point2D(R1.X, Y1);
                // calculate the length of the crossbar
                length = R1.X - X1; // length in mm
                // calculate the end points of the arrow features
                Point2D a1a = new Point2D(X1 + ARROWSIZE, Y1 + ARROWSIZE / 3);
                Point2D a1b = new Point2D(X1 + ARROWSIZE, Y1 - ARROWSIZE / 3);
                Point2D a2a = new Point2D(end.X - ARROWSIZE, end.Y + ARROWSIZE / 3);
                Point2D a2b = new Point2D(end.X - ARROWSIZE, end.Y - ARROWSIZE / 3);
                if (length < 0)
                {
                    // there must be a better way to do this but hey ho
                    length = -length;
                    // calculate the end points of the arrow features
                    a1a = new Point2D(X1 - ARROWSIZE, Y1 + ARROWSIZE / 3);
                    a1b = new Point2D(X1 - ARROWSIZE, Y1 - ARROWSIZE / 3);
                    a2a = new Point2D(end.X + ARROWSIZE, end.Y + ARROWSIZE / 3);
                    a2b = new Point2D(end.X + ARROWSIZE, end.Y - ARROWSIZE / 3);
                }
                string units;
                int Ilength;
                int Dlength;
                // convert length to string based on Units and precision
                switch(DimensionUnit)
                {
                    case "Mils":
                        length = length / 25.4 * 1000;
                        units = "mil";
                        break;
                    case "Millimeters":
                        units = "mm";
                        break;
                    case "Inches":
                        length = length /25.4;
                        units = "in";
                        break;
                    case "Centimeters":
                        length = length / 10;
                        units = "cm";
                        break;
                    default:// mm
                        units = "mm";
                        break;
                }

                length = Math.Round(length, TEXTPRECISION);
                Int32 MP = Convert.ToInt32(Math.Pow(10, TEXTPRECISION));
                Int32 FP = Convert.ToInt32(length * Math.Pow(10, TEXTPRECISION));

                Ilength = FP / MP;
                Dlength = FP - Ilength * MP;
                string Decimal = Dlength.ToString("D" + TEXTPRECISION);
                if(TEXTPRECISION==0)
                    text = $"{Ilength}{units}";
                else
                    text = $"{Ilength}.{Decimal}{units}";

                // rotate all the points back
                a1a = a1a.Rotate(centre, ANGLE);
                a1b = a1b.Rotate(centre, ANGLE);
                a2a = a2a.Rotate(centre, ANGLE);
                a2b = a2b.Rotate(centre, ANGLE);

                R0 = R0.Rotate(centre, ANGLE);
                R1 = R1.Rotate(centre, ANGLE);
                end = end.Rotate(centre, ANGLE);

                var string1 = new StringBuilder("");
                var Layers = Brd.GetLayers(Brd.GetLayer(layer));
                foreach (var L in Layers)
                {
                    string1.Append($@"
  (dimension 176 (width {LINEWIDTH}) (layer {L})
    (gr_text {text} (at {TEXT1X} {-TEXT1Y} {ANGLE}) (layer {L.ToString()})
        (effects (font (size {Math.Round(TEXTHEIGHT, Precision)} {Math.Round(TEXTHEIGHT, Precision)}) (thickness {Math.Round(LINEWIDTH, Precision)})) (justify left ))
    )
    (feature1 (pts (xy {end.X} {-end.Y}) (xy {R1.X} {-R1.Y})  ))
    (feature2 (pts  (xy {X1} {-Y1}) (xy {R0.X} {-R0.Y})))
    (crossbar (pts (xy {X1} {-Y1}) (xy {end.X} {-end.Y})))
    (arrow1a  (pts (xy {X1} {-Y1}) (xy {a1a.X} {-a1a.Y})))
    (arrow1b  (pts (xy {X1} {-Y1}) (xy {a1b.X} {-a1b.Y})))
    (arrow2a  (pts (xy {end.X} {-end.Y}) (xy {a2a.X} {-a2a.Y})))
    (arrow2b  (pts (xy {end.X} {-end.Y}) (xy {a2b.X} {-a2b.Y})))
  )");
                }
                CheckMinMax(X1, Y1);
                CheckMinMax(end.X, end.Y);
                CheckMinMax(a1a.X, a1a.Y);
                CheckMinMax(a1b.X, a1b.Y);
                CheckMinMax(a2a.X, a2a.Y);
                CheckMinMax(a2b.X, a2b.Y);

                return string1.ToString() ;
            }
        }

        // class for the dimensions document entry in the pcbdoc file
        class Dimensions : PcbDocEntry
        {
            public Dimensions(string filename, string cmfilename, string record, Type type, int offset) : base(filename, cmfilename, record, type, offset)
            {
            }

            public override bool ProcessLine(string line)
            {
                base.ProcessLine();
                if (line.Substring(0, 1) != "|")
                    return false;
                Dimension Dimension = new Dimension(line); //, l);
                DimensionsL.Add(Dimension);
                return true;
            }
        }
    }
}
