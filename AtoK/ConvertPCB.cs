using Ionic.Zlib;
using OpenMcdf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using AtoK;
using System.Threading;
using System.Windows.Forms;
using System.Drawing;

namespace ConvertToKicad
{
    public static class Globals
    {
        static public string ReportFilename;
        static public int ReportLines;
    }

    public partial class ConvertPCBDoc
    {
        public static bool ConvertRunning = false;
        public const int Precision = 3; // after the decimal point precision i.e. 3 digits
        static string net_classes = "";
        static StringBuilder tracks;
        static UInt32 track_count = 0;
        static StringBuilder texts;
        static StringBuilder arcs;
        static StringBuilder fills;
        static StringBuilder keepouts;
        public static bool ExtractFiles = false;
        public static bool CreateLib = false;
        public static bool Verbose = false;
        public static bool CMFile = false;
        static string CurrentLayer = "";
        static int CurrentModule;
        public static string filename = "";
        public static string output_filename = "";
        static char[] charsToTrim = { 'm', 'i', 'l' };
        static FileVersionInfo FV;
        static ObjectList<Net>       NetsL;
        static ObjectList<Module>    ModulesL;
        static ObjectList<Polygon>   PolygonsL;
        static ObjectList<Line>      LinesL;
        static ObjectList<Pad>       PadsL;
        static ObjectList<String>    Strings;
        static ObjectList<Via>       ViasL;
        static ObjectList<Fill>      FillsL;
        static ObjectList<Dimension> DimensionsL;
        static ObjectList<Rule>      RulesL;
        static ObjectList<Region>    RegionsL;
        static ObjectList<Line>      Segments;
        static Stopwatch StopWatch;
        static double originX = 0;
        static double originY = 0;
        static StreamWriter ReportFile;

        static void CheckThreadAbort(Exception Ex)
        {
            if (Ex.Message == "Thread was being aborted.")
            {
                throw Ex;
            }
            else
                OutputError($"{Ex.Message}"+Ex.StackTrace);
        }

        static void CheckThreadAbort(Exception Ex, string text)
        {
            //            int line = Convert.ToInt32(Ex.ToString().Substring(Ex.ToString().IndexOf("line")).Substring(0, Ex.ToString().Substring(Ex.ToString().IndexOf("line")).ToString().IndexOf("\r\n")).Replace("line ", ""));
            if (Ex.Message == "Thread was being aborted.")
            {
                throw Ex;
            }
            else
            {
                OutputError(Ex.Message);
                OutputError(text);
            }
        }

        // used to decompress the 3D models in the PcbDoc file
        static string ZlibCodecDecompress(byte[] compressed)
        {
            int outputSize = 2048;
            byte[] output = new Byte[outputSize];

            // If you have a ZLIB stream, set this to true.  If you have
            // a bare DEFLATE stream, set this to false.
            bool expectRfc1950Header = true;

            using (MemoryStream ms = new MemoryStream())
            {
                ZlibCodec compressor = new ZlibCodec();
                compressor.InitializeInflate(expectRfc1950Header);

                compressor.InputBuffer = compressed;
                compressor.AvailableBytesIn = compressed.Length;
                compressor.NextIn = 0;
                compressor.OutputBuffer = output;

                foreach (var f in new FlushType[] { FlushType.None, FlushType.Finish })
                {
                    int bytesToWrite = 0;
                    do
                    {
                        compressor.AvailableBytesOut = outputSize;
                        compressor.NextOut = 0;
                        compressor.Inflate(f);

                        bytesToWrite = outputSize - compressor.AvailableBytesOut;
                        if (bytesToWrite > 0)
                            ms.Write(output, 0, bytesToWrite);
                    }
                    while ((f == FlushType.None && (compressor.AvailableBytesIn != 0 || compressor.AvailableBytesOut == 0)) ||
                           (f == FlushType.Finish && bytesToWrite != 0));
                }

                compressor.EndInflate();

                return UTF8Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        // convert string such as "29470.3502mil" to millimeters
        // relative to the board origin X
        static double GetCoordinateX(string coord)
        {
            if (coord == "")
                return 0;
            string c = coord.Trim(charsToTrim);
            double C = Convert.ToDouble(c) * 25.4 / 1000.0;
            return Math.Round(C - originX, Precision);
        }

        // convert string such as "29470.3502mil" to millimeters
        // relative to the board origin Y
        static double GetCoordinateY(string coord)
        {
            if (coord == "")
                return 0;
            string c = coord.Trim(charsToTrim);
            double C = Convert.ToDouble(c) * 25.4 / 1000.0;
            return Math.Round(C - originY, Precision);
        }

        // return the length of a track
        static double Length(double x1, double y1, double x2, double y2)
        {
            double length = Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
            return length;
        }

        // find the string 's' within the string 'line' and return the remaining string up to the next '|'
        static public string GetString(string line, string s)
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
            return ToLiteral(param.TrimEnd('\0')); //return param.TrimEnd('\0'); 
        }

        // get unsigned 32 bit number
        static public UInt32 GetUInt32(string number)
        {
            if (number == "")
                return 0;
            return Convert.ToUInt32(number);
        }

        // get double number
        static public double GetDouble(string number)
        {
            if (number == "")
                return 0;
            return Math.Round(Convert.ToDouble(number.Trim(charsToTrim)), Precision);
        }

        static public string GetNetName(int net_no)
        {
            if (net_no == -1)
                return "\"\"";
            return $"\"{NetsL[net_no].Name}\"";
        }

        // Altium layer numbers
        public enum Layers
        {
            top_layer = 1,
            mid_1 = 2,
            mid_2 = 3,
            mid_3 = 4,
            mid_4 = 5,
            mid_5 = 6,
            mid_6 = 7,
            mid_7 = 8,
            mid_8 = 9,
            mid_9 = 10,
            mid_10 = 11,
            mid_11 = 12,
            mid_12 = 13,
            mid_13 = 14,
            mid_14 = 15,
            mid_15 = 16,
            mid_16 = 17,
            mid_17 = 18,
            mid_18 = 19,
            mid_19 = 20,
            mid_20 = 21,
            mid_21 = 22,
            mid_22 = 23,
            mid_23 = 24,
            mid_24 = 25,
            mid_25 = 26,
            mid_26 = 27,
            mid_27 = 28,
            mid_28 = 29,
            mid_29 = 30,
            mid_30 = 31,
            bottom_layer = 32,

            Top_Overlay = 33,
            Bottom_Overlay = 34,
            Top_Paste = 35,
            Bottom_Paste = 36,
            Top_Solder = 37,
            Bottom_Solder = 38,

            plane_1 = 39,
            plane_2 = 40,
            plane_3 = 41,
            plane_4 = 42,
            plane_5 = 43,
            plane_6 = 44,
            plane_7 = 45,
            plane_8 = 46,
            plane_9 = 47,
            plane_10 = 48,
            plane_11 = 49,
            plane_12 = 50,
            plane_13 = 51,
            plane_14 = 52,
            plane_15 = 53,
            plane_16 = 54,

            Drill_Guide = 55,
            Keepout_Layer = 56,

            Mech_1 = 57,
            Mech_2 = 58,
            Mech_3 = 59,
            Mech_4 = 60,
            Mech_5 = 61,
            Mech_6 = 62,
            Mech_7 = 63,
            Mech_8 = 64,
            Mech_9 = 65,
            Mech_10 = 66,
            Mech_11 = 67,
            Mech_12 = 68,
            Mech_13 = 69,
            Mech_14 = 70,
            Mech_15 = 71,
            Mech_16 = 72,

            Drill_Drawing = 73,
            Multi_Layer = 74
        }

        // Altium layer names
        static readonly string[] LayerNames =
        {
            "UNKNOWN",
            "TOP",
            "MID1",
            "MID2",
            "MID3",
            "MID4",
            "MID5",
            "MID6",
            "MID7",
            "MID8",
            "MID9",
            "MID10",
            "MID11",
            "MID12",
            "MID13",
            "MID14",
            "MID15",
            "MID16",
            "MID17",
            "MID18",
            "MID19",
            "MID20",
            "MID21",
            "MID22",
            "MID23",
            "MID24",
            "MID25",
            "MID26",
            "MID27",
            "MID28",
            "MID29",
            "MID30",
            "BOTTOM",
            "TOPOVERLAY",
            "BOTTOMOVERLAY",
            "TOPPASTE",
            "BOTTOMPASTE",
            "TOPSOLDER",
            "BOTTOMSOLDER",
            "PLANE1",
            "PLANE2",
            "PLANE3",
            "PLANE4",
            "PLANE5",
            "PLANE6",
            "PLANE7",
            "PLANE8",
            "PLANE9",
            "PLANE10",
            "PLANE11",
            "PLANE12",
            "PLANE13",
            "PLANE14",
            "PLANE15",
            "PLANE16",
            "DRILLGUIDE",
            "KEEPOUT",
            "MECHANICAL1",
            "MECHANICAL2",
            "MECHANICAL3",
            "MECHANICAL4",
            "MECHANICAL5",
            "MECHANICAL6",
            "MECHANICAL7",
            "MECHANICAL8",
            "MECHANICAL9",
            "MECHANICAL10",
            "MECHANICAL11",
            "MECHANICAL12",
            "MECHANICAL13",
            "MECHANICAL14",
            "MECHANICAL15",
            "MECHANICAL16",
            "DRILLDRAWING",
            "MULTILAYER",
            "UNKNOWN",
            "UNKNOWN",
            "UNKNOWN",
            "UNKNOWN",
            "UNKNOWN",
            "UNKNOWN",
            "UNKNOWN",
            "UNKNOWN",
            "UNKNOWN",
            "UNKNOWN",
            "UNKNOWN",
        };

        // convert Altium negation to Kicad negation
        // e.g. convert "M\_\W\E\" "~M_WE"
        static public string ConvertIfNegated(string Name)
        {
            if (Name.IndexOf('\\') == -1)
                return Name;
            // name is negated fully or in part
            // prepend ~ and another if negated chars end before end of string
            StringBuilder ret = new StringBuilder("");
            bool negating = false;
            for (var i = 0; i < Name.Length; i++)
            {
                if ((i != Name.Length - 1) && Name[i + 1] == '\\')
                {
                    if (!negating)
                    {
                        negating = true;
                        ret.Append('~');
                    }
                    ret.Append(Name[i]);
                    i++;
                }
                else
                {
                    if (negating)
                        negating = false;
                    ret.Append('~');
                    ret.Append(Name[i]);
                }
            }
            return ret.ToString();
        }

        class Point2D
        {
            public double X { get; set; }
            public double Y { get; set; }

            public Point2D()
            {
                X = 0;
                Y = 0;
            }

            public Point2D(double x, double y)
            {
                X = Math.Round(x, Precision);
                Y = Math.Round(y, Precision);
            }

            // rotate point around 0,0
            public void Rotate(double rotation)
            {
                rotation %= 360;
                if (rotation != 0)
                {
                    // convert to radians
                    rotation = rotation * Math.PI / 180;
                    double xp = X;
                    double yp = Y;
                    // do the rotation transform
                    X = Math.Round(xp * Math.Cos(rotation) - yp * Math.Sin(rotation), Precision);
                    Y = Math.Round(xp * Math.Sin(rotation) + yp * Math.Cos(rotation), Precision);
                }
            }

            public void Translate(double x, double y)
            {
                X += x; X = Math.Round(X, Precision);
                Y += y; Y = Math.Round(Y, Precision);
            }

            public Point2D Rotate(Point2D center, double angle)
            {
                angle = (angle) * (Math.PI / 180); // Convert to radians
                var RotatedX = Math.Cos(angle) * (X - center.X) - Math.Sin(angle) * (Y - center.Y) + center.X;
                var RotatedY = Math.Sin(angle) * (X - center.X) + Math.Cos(angle) * (Y - center.Y) + center.Y;
                return new Point2D(RotatedX, RotatedY);
            }
        }

        // class for point objects
        class Point : Object
        {
            public readonly double X, Y;

            private Point()
            { }

            public Point(double x, double y)
            {
                X = x;
                Y = y;
            }

            public override string ToString()
            {
                return $"(xy {X} {-Y}) ";
            }

            public override string ToString(double x, double y, double ModuleRotation)
            {
                double angle = (ModuleRotation) * (Math.PI / 180); // Convert to radians
                double RotatedX = Math.Round(Math.Cos(angle) * (X - x) - Math.Sin(angle) * (Y - y), Precision);
                double RotatedY = Math.Round(Math.Sin(angle) * (X - x) + Math.Cos(angle) * (Y - y), Precision);
                return $"(xy {RotatedX} {-RotatedY}) ";
            }


        }

        // convert Altium mil value string to millimeters
        static public double GetNumberInMM(string number)
        {
            if (number == "")
                return 0;
            return Math.Round(Convert.ToDouble(number.Trim(charsToTrim)) * 25.4 / 1000.0, Precision);
        }

        // convert Altium int mil value to mm
        static public double ToMM(int val)
        {
            return Math.Round((double)val * 25.4 / 10000000, Precision);
        }

        // convert Altium double mil value to mm 
        static public double ToMM(double val)
        {
            return Math.Round(val * 25.4 / 10000000, Precision);
        }

        // convert 32 bit fixed point value in mil to mm 
        static public double ToMM(byte[] arr, int pos)
        {
            Int32 val = arr[pos] + (arr[pos + 1] << 8) + (arr[pos + 2] << 16) + (arr[pos + 3] << 24);
            return Math.Round((double)val * 25.4 / 10000000, Precision);
        }

        // convert 32 bit fixed point value in mil to mm 
        static public double Bytes2mm(byte[] arr)
        {
            // altium store numbers as fixed point /10000 to get value in mils
            Int32 val = arr[0] + (arr[1] << 8) + (arr[2] << 16) + (arr[3] << 24);
            return Math.Round((double)val * 25.4 / 10000000, Precision);
        }

        // convert byte array to unsigned 16 bit integer
        static public UInt16 B2UInt16(byte[] arr, int pos)
        {
            return (UInt16)(arr[pos] + (arr[pos + 1] << 8));
        }

        // convert byte array to 16 bit integer
        static public Int16 B2Int16(byte[] arr, int pos)
        {
            return (Int16)(arr[pos] + (arr[pos + 1] << 8));
        }

        // convert byte array to structure
        public static unsafe T ByteArrayToStructure<T>(byte[] bytes) where T : struct
        {
            fixed (byte* ptr = &bytes[0])
            {
                return (T)Marshal.PtrToStructure((IntPtr)ptr, typeof(T));
            }
        }

        enum Type { text, binary, mixed, special };

        // base class for the different document entries in the pcbdoc file
        class PcbDocEntry
        {
            public string FileName { get; set; }
            public string CMFileName { get; set; }
            public string Record { get; set; }
            public Type Type { get; set; }
            private readonly int offset;
            public UInt32 Binary_size { get; set; }
            private int Processed;
            private Stopwatch watch;
            List<byte[]> binary;

            public PcbDocEntry()
            {
                Processed = 0;
            }

            public void StartTimer()
            {
                watch.Start();
            }

            public PcbDocEntry(string filename, string cmfilename, string record, Type type, int off)
            {
                binary = new List<byte[]>();
                FileName = filename;
                CMFileName = cmfilename;
                Record = record;
                Type = type;
                offset = off;
                Binary_size = 0;
                watch = new Stopwatch();
            }

            public virtual bool ProcessBinaryFile(byte[] data)
            {
                if (Binary_size == 0)
                    return false;

                watch.Start();
                MemoryStream ms = new MemoryStream(data);
                long size = ms.Length;
                if (size == 0)
                    return true;

                UInt32 pos = 0;
                // record consists of
                // byte type
                // int32 offset - this plus 5 is the offset of the next record
                // record
                // so record size is offset + 5

                BinaryReader br = new BinaryReader(ms, System.Text.Encoding.UTF8);
                ms.Seek(1, SeekOrigin.Begin);
                Binary_size = (UInt32)br.ReadInt32();
                Binary_size += 5;
                while (pos < size)
                {
                    ms.Seek(pos, SeekOrigin.Begin);
                    byte[] line = br.ReadBytes((int)Binary_size);
                    binary.Add(line);
                    ProcessLine(line);
                    pos += Binary_size;
                }
                return true;

            }

            public virtual bool ProcessFile(byte[] data)
            {
                watch.Start();
                if ((Binary_size != 0) && (Type == Type.binary))
                {
                    return ProcessBinaryFile(data);
                }

                if (Type != Type.text)
                    return false;

                using (MemoryStream ms = new MemoryStream(data))
                {
                    UInt32 pos = 0;
                    long size = ms.Length;

                    if (size == 0)
                        return false;

                    BinaryReader br = new BinaryReader(ms, System.Text.Encoding.UTF8);
                    ms.Seek(0, SeekOrigin.Begin);
                    while (pos + 4 < size)
                    {
                        ms.Seek(pos + offset - 4, SeekOrigin.Begin);
                        uint next = br.ReadUInt32();
                        if (pos + next > size)
                        {
                            // obviously erroneous value for next so exit
                            break;
                        }
                        char[] line = br.ReadChars((int)next);

                        string str = new string(line);
                        if (str.Length > 10) // fudge
                            ProcessLine(str.TrimEnd('\0'));
                        pos += (next + (UInt32)offset);
                    }
                }

                return true;
            }

            public virtual bool ProcessFile()
            {
                watch.Start();
                return true;
            }

            public virtual bool ProcessLine(string line)
            {
                return true;
            }

            public virtual bool ProcessLine()
            {
                Processed++;
                return true;
            }


            public string ConvertToString(byte[] bytes)
            {
                return new string(bytes.Select(Convert.ToChar).ToArray());
            }


            public virtual bool ProcessLine(byte[] line)
            {
                Processed++;
                return true;
            }

            // do any processing required at end of file processing
            public virtual void FinishOff()
            {
                if (!Program.ConsoleApp)
                    // show progress if GUI
                    OutputString($"Processed {Processed} Objects in {GetTimeString(watch.ElapsedMilliseconds)}");
            }
        }

        abstract class Object
        {
            public Object()
            { }

            public virtual string ToString(double x, double y, double rotation)
            {
                return "";
            }
        }

        // class for creating lists of objects
        class ObjectList<T> : List<T> where T : Object
        {

            public ObjectList() : base()
            {
            }

            public new void Add(T newobj)
            {
                base.Add(newobj);
            }

            public override string ToString()
            {
                StringBuilder ret = new StringBuilder("");
                try
                {
                    for (var i = 0; i < base.Count; i++)
                    {
                        string type = $"{base[i].GetType()}";
                        if (type.IndexOf("Module") != -1)
                        {
                            CurrentModule = i;
                        }
                        ret.Append(base[i].ToString());
                    }
                }
                catch (Exception Ex)
                {
                    CheckThreadAbort(Ex);
                }
                return ret.ToString();
            }

            public string ToString(double x, double y, double rotation)
            {
                StringBuilder ret = new StringBuilder("");
                for (var i = 0; i < base.Count; i++)
                {
                    ret.Append(base[i].ToString(x, y, rotation));
                }
                return ret.ToString();
            }
        }

        static List<PcbDocEntry> PcbObjects;

        static bool MakeDir(string name)
        {
            if (!Directory.Exists(name))
            {
                // create the library directory
                DirectoryInfo info = Directory.CreateDirectory(name);
                if (!info.Exists)
                {
                    // for some reason the create directory failed so try again
                    info = Directory.CreateDirectory(name);
                    Thread.Sleep(500); // TODO this is a frig sort out
                    if (!Directory.Exists(name))
                    {
                        OutputError($@"failed to create directory ""{name}""");
                        return false;
                    }
                }
            }
            return true;
        }

        static void ProcessPcbObject(CompoundFile cf, PcbDocEntry Object)
        {
            CFStorage storage=null;
            CMFile = false;
            if (!CMFile)
            {
                // try normal directory name
                if ((storage = cf.RootStorage.TryGetStorage(Object.FileName)) != null)
                {
                    OutputString($"Processing {Object.FileName}");
                }
                else
                // try CM type directory name
                if ((storage = cf.RootStorage.TryGetStorage(Object.CMFileName)) != null)
                {
                    OutputString($"Processing {Object.FileName} aliased as {Object.CMFileName}");
                }
                else
                {
                    // try version 5 directory name
                    if (Object.FileName[Object.FileName.Length - 1] == '6')
                    {
                        filename = Object.FileName.Substring(0, Object.FileName.Length - 1);
                        if ((storage = cf.RootStorage.TryGetStorage(filename)) != null)
                        {
                            OutputString($"Processing {filename}");
                        }
                        else
                        {
                            OutputString($"Didn't find '{Object.FileName}' or any other candidates");
                            return;
                        }
                    }
                    else
                    {
                        OutputString($"Didn't find '{Object.FileName}' or any other candidates");
                        return;
                    }
                }
            }
            if (MakeDir(Object.FileName)) // storage.Name))
            {
                Directory.SetCurrentDirectory(@".\" + Object.FileName); // storage.Name);

                string dir = Directory.GetCurrentDirectory();

                if (Object.FileName == "Models" && !CMFile)
                {
                    // extract the model files to 0.dat,1.dat...
                    // this is called for each of the directory entries
                    void vs(CFItem it)
                    {
                        // write all entries (0,1,2..n,Data) to files (0.dat,1.dat...)
                        if (it.Name != "Header")
                        {
                            // get file contents and write to file
                            CFStream fstream = it as CFStream;

                            byte[] temp = fstream.GetData();
                            try
                            {
                                File.WriteAllBytes($"{it.Name}.dat", temp);
                            }
                            catch (Exception Ex)
                            {
                                CheckThreadAbort(Ex);
                            }
                            string filename = $"{it.Name}.dat";
                            if (filename != "Data.dat")
                            {
                                // uncompress the x.dat file to a .step file
                                // step file is renamed to its actual name later in the process
                                byte[] compressed = File.ReadAllBytes(filename);
                                string Inflated = ZlibCodecDecompress(compressed);

                                File.WriteAllText(it.Name + ".step", Inflated);
                                File.Delete(filename); // no longer need .dat file
                            }
                        }
                    }

                    // calls the Action delegate for each of the directory entries
                    storage.VisitEntries(vs, true);
                }

                if (ExtractFiles && !CMFile)
                {
                    try
                    {
                        // get file contents and write to file
                        //byte[] temp = ;
                        File.WriteAllBytes("Data.dat", storage.GetStream("Data").GetData());
                    }
                    catch (Exception Ex)
                    {
                        CheckThreadAbort(Ex);
                    }
                }
                /*CFStream*/
                try
                {
                    byte[] data;
                    if (!CMFile)
                    {
                        //CFStream stream;
                        //stream = storage.GetStream("Data");
                        // get file contents and process
                        data = storage.GetStream("Data").GetData();
                    }
                    else
                    {
                        string dir2 = Directory.GetCurrentDirectory();
                        // get the data from the extracted file
                        data = File.ReadAllBytes("Data.dat");
                    }
                    if (Object.Binary_size != 0)
                        Object.ProcessBinaryFile(data);
                    else
                        Object.ProcessFile(data);
                }
                catch (Exception Ex)
                {
                    string where = Directory.GetCurrentDirectory();

                    CheckThreadAbort(Ex);
                }

                Object.FinishOff();
                Directory.SetCurrentDirectory(@"..\");
                string Dir = Directory.GetCurrentDirectory();
            }
        }

        static Board Brd;

        public static void ClearFolder(string FolderName)
        {
            try
            {
                DirectoryInfo dir = new DirectoryInfo(FolderName);

                // delete any files
                foreach (FileInfo fi in dir.GetFiles())
                {
                    try
                    {
                        fi.Delete();
                        fi.Refresh();
                        // sometimes needs time for file to be deleted
                        while (fi.Exists)
                        {
                            System.Threading.Thread.Sleep(100);
                            fi.Refresh();
                        }
                    }
                    catch(IOException Ex)
                    {
                        OutputError($"Couldn't delete {FolderName}\\{fi.Name}");
                    }
                }

                // delete any subdirectories
                foreach (DirectoryInfo di in dir.GetDirectories())
                {
                    // recurse
                    ClearFolder(di.FullName);
                    try
                    {
                        di.Delete();
                        di.Refresh();
                        // sometimes needs time for file to be deleted
                        while (di.Exists)
                        {
                            System.Threading.Thread.Sleep(100);
                            di.Refresh();
                        }
                    }
                    catch (IOException Ex)
                    {
                        OutputError($"Couldn't delete {di.FullName}", true);
                    }
                }
            }
            catch (Exception Ex)
            {
                CheckThreadAbort(Ex);
            }
        }

        public static void OutputString(string text)
        {
            if (Program.ConsoleApp)
            {
                if (Verbose)
                    Debug.WriteLine(text);
            }
            else
            {
                if (Verbose)
                {
                    Program.Form.outputList_Add(text, System.Drawing.Color.Black);
                    Program.Form.outputList_Update();
                }
            }
        }

        public static void OutputError(string text)
        {
            // always error text
            if (Program.ConsoleApp)
            {
                Console.Error.WriteLine(text);
            }
            else
            {
                Globals.ReportLines++;
                if(ConvertRunning && ReportFile != null)
                    ReportFile.WriteLine(text);
                else
                {
                    Program.Form.outputList_Add(text, System.Drawing.Color.Red);
                    Program.Form.outputList_Update();
                }
            }
        }

        public static void OutputError(string text, bool Screen)
        {
            // always error text
            if (Program.ConsoleApp)
            {
                Console.Error.WriteLine(text);
            }
            else
            {
                Program.Form.outputList_Add(text, System.Drawing.Color.Red);
                Program.Form.outputList_Update();
            }
        }

        static public string GetTimeString(double time)
        {
            if (time > 1000)
                return $"{time / 1000} S";
            else
                return $"{time} mS";
        }

        public unsafe void ConvertFile(string FileName, bool extractFiles, bool createLib)
        {
            net_classes = "";
            tracks      = new StringBuilder("");
            texts       = new StringBuilder("");
            arcs        = new StringBuilder("");
            fills       = new StringBuilder("");
            keepouts    = new StringBuilder("");
            StopWatch = new Stopwatch();
            Globals.ReportLines = 0;

            StopWatch.Start();
            try
            {
                Brd            = new Board();
                ModulesL       = new ObjectList<Module>();
                NetsL          = new ObjectList<Net>();
                PolygonsL      = new ObjectList<Polygon>();
                LinesL         = new ObjectList<Line>();
                PadsL          = new ObjectList<Pad>();
                Strings        = new ObjectList<String>();
                ViasL          = new ObjectList<Via>();
                FillsL         = new ObjectList<Fill>();
                DimensionsL    = new ObjectList<Dimension>();
                RulesL         = new ObjectList<Rule>();
                Mods           = new Models();
                ShapeBasedMods = new ShapeBasedModels();
                RegionsL       = new ObjectList<Region>();
                Segments       = new ObjectList<Line>();


                OutputString("Starting");
                ExtractFiles = extractFiles;
                CreateLib = createLib;
                // start off the nets
                Net NoNet = new Net(0, "");
                NetsL.Add(NoNet);

                filename = FileName;

                if (!File.Exists(filename))
                {
                    OutputError($"File \"{filename}\" doesn't exist");
                    if (Program.ConsoleApp)
                        System.Environment.Exit(0);
                    else
                        return;
                }

                string Extension = "";
                if ((filename.Length - filename.IndexOf(".pcbdoc", StringComparison.OrdinalIgnoreCase)) == 7)
                {
                    Extension = ".pcbdoc";
                    CMFile = false;
                }
                else
                if ((filename.Length - filename.IndexOf(".cmpcbdoc", StringComparison.OrdinalIgnoreCase)) == 9)
                {
                    Extension = ".cmpcbdoc";
                    CMFile = true;
                }

                if (Extension == "")
                {
                    OutputError($"File {filename} should end in '.pcbdoc' or '.cmpcbdoc'");
                    System.Environment.Exit(0);
                }

                int index = filename.LastIndexOf('.');
                //            output_filename = filename.Substring(0,index) + ".kicad_pcb";
                if (index == -1)
                {
                    OutputError($"File {filename} is not valid pcb file");
                    System.Environment.Exit(0);
                }

                //if (filename.Substring(index, filename.Length - index).ToLower() != ".pcbdoc")
                if (Extension == "")
                {
                    OutputError($"File {filename} is not valid pcb file");
                    System.Environment.Exit(0);
                }

                CompoundFile cf = new CompoundFile(filename);

                FV = new FileVersionInfo("FileVersionInfo", "38434311D9D84DCDB403436D1149A8", "", Type.text, 4);
                CFStorage storage = cf.RootStorage.TryGetStorage(FV.FileName);
                if (storage == null)
                {
                    storage = cf.RootStorage.TryGetStorage(FV.CMFileName);
                    if (storage == null)
                    {

                    //    OutputError("Unsupported file format...Pre Winter09 file");
                        Globals.ReportLines = 0;
                    //    return;
                    }
                }

                ConvertRunning = true;

                // Initialise the PcbObjects list
                PcbObjects = new List<PcbDocEntry>
                {
                    FV,
                    new Board                    ("Board6",                       "96B09F5C6CEE434FBCE0DEB3E88E70", "Board",                    Type.text,   4),
                    new Rules                    ("Rules6",                       "C27718A40C94421388FAE5BD7785D7", "Rule",                     Type.text,   6),
                    new Classes                  ("Classes6",                     "4F71DD45B09143988210841EA1C28D", "Class",                    Type.text,   4),
                    new Nets                     ("Nets6",                        "35D7CF51BB9B4875B3A138B32D80DC", "Net",                      Type.text,   4),
                    new DifferentialPairs        ("DifferentialPairs6",           "17DC1EE78CF64F22A78C16A208DE80", "DifferentialPair",         Type.text,   4),
                    new Components               ("Components6",                  "F9D060ACC7DD4A85BC73CB785BAC81", "Component",                Type.text,   4),
                    new Polygons                 ("Polygons6",                    "A1931C8B0B084A61AA45146575FDD3", "Polygon",                  Type.text,   4),
                    new Dimensions               ("Dimensions6",                  "068B9422DBB241258BA2DE9A6BA1A6", "Embedded",                 Type.text,   6),
                    new Arcs                     ("Arcs6",                        "1CEEB63FB33847F8AFC4485F64735E", "Arc",                      Type.binary, 4),
                    new Pads                     ("Pads6",                        "4F501041A9BC4A06BDBDAB67D3820E", "Pad",                      Type.binary, 4),
                    new Vias                     ("Vias6",                        "C87A685A0EFA4A90BEEFD666198B56", "Via",                      Type.binary, 4),
                    new Tracks                   ("Tracks6",                      "412A754DBB864645BF01CD6A80C358", "Track",                    Type.binary, 4),
                    new Texts                    ("Texts6",                       "A34BC67C2A5F408D8F377378C5C5E2", "Text",                     Type.binary, 4),
                    new Fills                    ("Fills6",                       "6FFE038462A940E9B422EFC8F5D85E", "Fill",                     Type.binary, 4),
                    new Regions                  ("Regions6",                     "F513A5885418472886D3EF18A09E46", "Region",                   Type.binary, 4),
                    new Models6                  ("Models",                       "0DB009C021D946C88F1B3A32DAE94B", "ComponentBody",            Type.text,   4),
                    new ComponentBodies          ("ComponentBodies6",             "A0DB41FBCB0D49CE8C32A271AA7EF5", "ComponentBody",            Type.binary, 4),
                    new ShapeBasedComponentBodies("ShapeBasedComponentBodies6",   "44D9487C98CE4F0EB46AB6E9CDAF40", "ComponentBody",            Type.binary, 4)
                /* Not interested in the rest of these
                    , 
                    new AdvancedPlacerOptions    ("Advanced Placer Options6",     "90F01116977A40EF9F7CD92931AB45", "AdvancedPlacerOptions",    Type.text,   4), // not used
                    new PinSwapOptions           ("Pin Swap Options6",            "FF6E17E422A54417A1453ECEE2408E", "PinSwapOptions",           Type.text,   4), // not used
                    new DesignRuleCheckerOptions ("Design Rule Checker Options6", "0A342FA35A2D4FCDB8D2187D411EBC", "DesignRuleCheckerOptions", Type.text,   4), // not used
                    new EmbeddedFonts            ("EmbeddedFonts6",               "", "",                         Type.binary, 4),
                    new ShapeBasedRegions        ("ShapeBasedRegions6",           "", "",                         Type.mixed,  4),
                    new Connections              ("Connections6",                 "", "",                         Type.binary, 4),
                    new Coordinates              ("Coordinates6",                 "", "",                         Type.binary, 4),
                    new Embeddeds                ("Embeddeds6",                   "", "",                         Type.binary, 4),
                    new EmbeddedBoards           ("EmbeddedBoards6",              "", "",                         Type.binary, 4),
                    new FromTos                  ("FromTos6",                     "", "",                         Type.binary, 4),
                    new ModelsNoEmbeds           ("ModelsNoEmbed",                "", "",                         Type.binary, 4),
                    new Textures                 ("Textures",                     "", "",                         Type.binary, 4)
                */
                };

                // Boards6 is processed first so will have layer stack set up in it
                Brd = (Board)PcbObjects[1];

                if (filename.Contains("CMPCBDoc"))
                {
                    //                    CMConvert(cf); // descramble file names
                }
                OutputString($"Converting {filename}");
                string CM = (filename.Contains("CMPCBDoc")) ? "-CM" : "";
                string UnpackDirectory = filename.Substring(0, filename.LastIndexOf('.')) + CM + "-Kicad";
                OutputString($"Output Directory \"{UnpackDirectory}\"");
                if (!Directory.Exists(UnpackDirectory))
                {
                    // create the output directory
                    Directory.CreateDirectory(UnpackDirectory);
                }

                ReportFile = null;
                // clear out the directory
                ClearFolder(UnpackDirectory);

                // change to the directory
                Directory.SetCurrentDirectory(UnpackDirectory);
                string cd = Directory.GetCurrentDirectory();
                Globals.ReportFilename = cd + "\\Report.txt";
                ReportFile = File.CreateText("Report.txt");

                IList<IDirectoryEntry> entries = cf.GetDirectories();
                if (MakeDir(cf.RootStorage.Name))
                {
                    Directory.SetCurrentDirectory(".\\" + cf.RootStorage.Name);

                    foreach (var Object in PcbObjects)
                    {
                        ProcessPcbObject(cf, Object);
                    }
                }
                cf.Close();
                // sort out the 3D models
                if (Directory.Exists("Models"))
                {
                    StopWatch.Start();
                    Directory.SetCurrentDirectory("Models");
                    ProcessModelsFile("Data.dat");
                    if (!ExtractFiles)
                    {
                        File.Delete("Data.dat");
                        if (File.Exists("Data.txt"))
                            File.Delete("Data.txt");
                    }
                    Directory.SetCurrentDirectory(@"..\..");
                    try
                    {
                        if (Directory.Exists("Models"))
                            Directory.Delete("Models", true);
                    }
                    catch (Exception Ex)
                    {
                        CheckThreadAbort(Ex);
                    }
                    Directory.Move(@"Root Entry\Models", "Models");
                    if (!Directory.EnumerateFileSystemEntries("Models").Any())
                    {
                        // Models directory is empty so get rid
                        Directory.Delete("Models", true);
                    }
                    OutputString($"3D models extracted in {GetTimeString(StopWatch.ElapsedMilliseconds)}");
                }
                if (!ExtractFiles)
                {
                    Directory.Delete("Root Entry", true);
                    OutputString("Removing extracted files");
                }

                output_filename = FileName;
                int idx = output_filename.LastIndexOf('\\');
                if (idx == -1)
                    idx = 0;
                else
                    idx++;

                output_filename = output_filename.Substring(idx, output_filename.Length - idx);

                idx = output_filename.LastIndexOf('.');
                if(idx!=-1)
                    output_filename = output_filename.Substring(0, idx) + ".kicad_pcb";
                OutputString($"Outputting Kicad PCB file {UnpackDirectory}\\{output_filename}");
                System.IO.StreamWriter OutFile = new System.IO.StreamWriter(UnpackDirectory + "\\" + output_filename);

                // TODO find extremes of pcb to select correct page size
                {
                    OutFile.WriteLine("(kicad_pcb (version 4) (host pcbnew \"(2014 - 07 - 21 BZR 5016) - product\")");
                    OutFile.WriteLine("");
                    OutFile.WriteLine("  (general");
                    OutFile.WriteLine("    (links 0)");
                    OutFile.WriteLine("    (no_connects 0)");
                    OutFile.WriteLine("    (area 0 0 0 0)");
                    OutFile.WriteLine("    (thickness 1.6)");
                    OutFile.WriteLine("    (drawings 0)");
                    OutFile.WriteLine($"    (tracks {track_count})");
                    OutFile.WriteLine($"    (zones {PolygonsL.Count})");
                    OutFile.WriteLine($"    (modules {ModulesL.Count})");
                    OutFile.WriteLine($"    (nets {NetsL.Count})");
                    OutFile.WriteLine("  )");
                    OutFile.WriteLine("");
                    OutFile.WriteLine("  (page A4)");
                    OutFile.WriteLine("  (layers");
                    // output the layer stack
                    OutFile.WriteLine(Brd.ToString());
                    OutFile.WriteLine("    (32 B.Adhes user hide)");
                    OutFile.WriteLine("    (33 F.Adhes user hide)");
                    OutFile.WriteLine("    (34 B.Paste user hide)");
                    OutFile.WriteLine("    (35 F.Paste user hide)");
                    OutFile.WriteLine("    (36 B.SilkS user)");
                    OutFile.WriteLine("    (37 F.SilkS user)");
                    OutFile.WriteLine("    (38 B.Mask user hide)");
                    OutFile.WriteLine("    (39 F.Mask user hide)");
                    OutFile.WriteLine("    (40 Dwgs.User user hide)");
                    OutFile.WriteLine("    (41 Cmts.User user hide)");
                    OutFile.WriteLine("    (42 Eco1.User user hide)");
                    OutFile.WriteLine("    (43 Eco2.User user hide)");
                    OutFile.WriteLine("    (44 Edge.Cuts user)");
                    OutFile.WriteLine("    (45 Margin user)");
                    OutFile.WriteLine("    (46 B.CrtYd user hide)");
                    OutFile.WriteLine("    (47 F.CrtYd user hide)");
                    OutFile.WriteLine("    (48 B.Fab user hide)");
                    OutFile.WriteLine("    (49 F.Fab user hide)");
                    OutFile.WriteLine("");
                    OutFile.WriteLine("  )");
                    OutFile.WriteLine("");
                    OutFile.WriteLine("  (setup");
                    OutFile.WriteLine("    (last_trace_width 0.254)");
                    OutFile.WriteLine("    (trace_clearance 0.127)");
                    OutFile.WriteLine("    (zone_clearance 0.0144)");
                    OutFile.WriteLine("    (zone_45_only no)");
                    OutFile.WriteLine("    (trace_min 0.254)");
                    OutFile.WriteLine("    (segment_width 0.2)");
                    OutFile.WriteLine("    (edge_width 0.1)");
                    OutFile.WriteLine("    (via_size 0.889)");
                    OutFile.WriteLine("    (via_drill 0.635)");
                    OutFile.WriteLine("    (via_min_size 0.889)");
                    OutFile.WriteLine("    (via_min_drill 0.508)");
                    OutFile.WriteLine("    (uvia_size 0.508)");
                    OutFile.WriteLine("    (uvia_drill 0.127)");
                    OutFile.WriteLine("    (uvias_allowed no)");
                    OutFile.WriteLine("    (uvia_min_size 0.508)");
                    OutFile.WriteLine("    (uvia_min_drill 0.127)");
                    OutFile.WriteLine("    (pcb_text_width 0.3)");
                    OutFile.WriteLine("    (pcb_text_size 1.5 1.5)");
                    OutFile.WriteLine("    (mod_edge_width 0.15)");
                    OutFile.WriteLine("    (mod_text_size 1 1)");
                    OutFile.WriteLine("    (mod_text_width 0.15)");
                    OutFile.WriteLine("    (pad_size 1.5 1.5)");
                    OutFile.WriteLine("    (pad_drill 0.6)");
                    OutFile.WriteLine("    (pad_to_mask_clearance 0.1)"); // TODO should get value from design rules
                    OutFile.WriteLine("    (aux_axis_origin 0 0)");
                    OutFile.WriteLine("    (visible_elements 7FFFF77F)"); // TODO find out what this should be
                    OutFile.WriteLine("    (pcbplotparams");
                    OutFile.WriteLine("      (layerselection 262143)"); // TODO and this
                    OutFile.WriteLine("      (usegerberextensions false)");
                    OutFile.WriteLine("      (excludeedgelayer true)");
                    OutFile.WriteLine("      (linewidth 0.100000)");
                    OutFile.WriteLine("      (plotframeref false)");
                    OutFile.WriteLine("      (viasonmask false)");
                    OutFile.WriteLine("      (mode 1)");
                    OutFile.WriteLine("      (useauxorigin false)");
                    OutFile.WriteLine("      (hpglpennumber 1)");
                    OutFile.WriteLine("      (hpglpenspeed 20)");
                    OutFile.WriteLine("      (hpglpendiameter 15)");
                    OutFile.WriteLine("      (hpglpenoverlay 2)");
                    OutFile.WriteLine("      (psnegative false)");
                    OutFile.WriteLine("      (psa4output false)");
                    OutFile.WriteLine("      (plotreference true)");
                    OutFile.WriteLine("      (plotvalue true)");
                    OutFile.WriteLine("      (plotinvisibletext false)");
                    OutFile.WriteLine("      (padsonsilk false)");
                    OutFile.WriteLine("      (subtractmaskfromsilk false)");
                    OutFile.WriteLine("      (outputformat 1)");
                    OutFile.WriteLine("      (mirror false)");
                    OutFile.WriteLine("      (drillshape 0)");
                    OutFile.WriteLine("      (scaleselection 1)");
                    OutFile.WriteLine("      (outputdirectory \"GerberOutput/\"))");
                    OutFile.WriteLine("  )");
                    OutFile.WriteLine("");
                }

                OutFile.WriteLine(NetsL.ToString());
                OutFile.WriteLine(net_classes);
                OutFile.WriteLine(Brd.OutputBoardOutline());
                OutFile.WriteLine(ModulesL.ToString());
                //            PadsL.ToString(); N.B. KiCad doesn't allow free standing pads yet ...TODO create modules to do this
                OutFile.WriteLine(PolygonsL.ToString());
                OutFile.WriteLine(ViasL.ToString());
                OutFile.WriteLine(tracks.ToString());
                OutFile.WriteLine(arcs.ToString());
                OutFile.WriteLine(texts.ToString());
                OutFile.WriteLine(fills.ToString());
                OutFile.WriteLine(keepouts.ToString());
                OutFile.WriteLine(DimensionsL.ToString());
                OutFile.WriteLine(RegionsL.ToString());
                OutFile.WriteLine(")");

                OutFile.Close();

                if (CreateLib)
                {
                    OutputString("Making Library");
                    Directory.SetCurrentDirectory(UnpackDirectory);

                    // now create library directory and fill with modules
                    // now make directory based on the PCB filename
                    string fileN = Path.GetFileName(filename);
                    string dir = fileN.Substring(0, fileN.ToLower().IndexOf(".PcbDoc".ToLower())) + ".pretty";
                    if (!Directory.Exists(dir))
                    {
                        // create the library directory
                        Directory.CreateDirectory(dir);
                    }
                    // change to the directory
                    Directory.SetCurrentDirectory(dir);

                    List<Module> UniqueModules = new List<Module>();

                    // build list of unique modules
                    foreach (var Mod in ModulesL)
                    {
                        if (!InList(Mod, UniqueModules))
                        {
                            UniqueModules.Add(Mod);
                        }
                    }

                    // write out each of the unique modules into the library
                    foreach (var Mod in UniqueModules)
                    {
                        string fileName = "";
                        try
                        {
                            bool InvalidFilename = false;
                            fileName = Mod.Name + ".kicad_mod";
                            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                            {
                                if (fileName.Contains(c))
                                {
                                    fileName = fileName.Replace(c, '_');
                                    InvalidFilename = true;
                                }
                            }
                            if (InvalidFilename)
                            {
                                OutputError($"Invalid filename {Mod.Name}.kicad_mod changed to {fileName}");
                            }
                            // Check if file already exists. If yes, delete it.     
                            if (File.Exists(fileName))
                            {
                                File.Delete(fileName);
                            }

                            // Create a new file     
                            using (System.IO.StreamWriter file = new System.IO.StreamWriter(fileName))
                            {
                                file.Write(Mod.ToModule());
                                file.Close();
                            }
                        }
                        catch (Exception Ex)
                        {
                            CheckThreadAbort(Ex);
                        }
                    }
                }
                cf.Close();
                ReportFile.Close();
                Directory.SetCurrentDirectory("..\\");
                OutputString($"Finished time taken = {GetTimeString(StopWatch.ElapsedMilliseconds)}");
            }
            catch (ThreadAbortException abortException)
            {
                ConvertRunning = false;
                OutputString("Aborted");
                Directory.SetCurrentDirectory("..\\..");
            }
            ConvertRunning = false;
        }
    }
}

