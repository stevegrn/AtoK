using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ConvertToKicad
{
    public partial class ConvertPCBDoc
    {
        public static bool Post09;
        // class to hold version info
        class Version
        {
            public string VER { get; set; }
            //public string FWDMSG { get; set; }
            //public string BKMSG { get; set; }

            Version()
            {
                VER = "";
                Post09 = false;
                //FWDMSG = "";
                //BKMSG = "";
            }

            public Version(string ver, string fwdmsg, string bkmsg)
            {
                VER = ver.Substring(ver.IndexOf('=') + 1);
                Post09 = false;
                //FWDMSG = fwdmsg.Substring(fwdmsg.IndexOf('=')+1);
                //BKMSG  = bkmsg.Substring(bkmsg.IndexOf('=')+1);
            }

            public override string ToString()
            {
                return $"{VER}";
            }
        }

        class FileVersionInfo : PcbDocEntry
        {
            private int Count;
            private List<Version> Versions;

            public class VersionParam
            {
                public string Version { get; set; }
                public int PadOffset { get; set; }

                public VersionParam()
                {
                    Version = "";
                    PadOffset = 0;
                }

                public VersionParam(string version, int padOffset)
                {
                    Version = version;
                    PadOffset = padOffset;
                }

                int GetPadOffset()
                {
                    return 141;
                }

            }

            public VersionParam VP { get; set; }

            static readonly List<VersionParam> VersionParamL = new List<VersionParam>
            {
                new VersionParam("FileVersionInfo", 141),
                new VersionParam("6.3", 141),
                new VersionParam("6.6", 141),
                new VersionParam("6.8", 141),
                new VersionParam("6.8", 141),
                new VersionParam("6.8", 141),
                new VersionParam("6.8", 141),
                new VersionParam("6.8", 141),
                new VersionParam("6.8", 141),
                new VersionParam("6.9", 141),
                new VersionParam("6.9", 141),
                new VersionParam("7.0", 141),
                new VersionParam("Winter 09,", 141),
                new VersionParam("Winter 09", 141),
                new VersionParam("Winter 09", 141),
                new VersionParam("Winter 09", 141),
                new VersionParam("Summer 09", 141),
                new VersionParam("Summer 09", 141),
                new VersionParam("Summer 09", 141),
                new VersionParam("Release 10", 141),
                new VersionParam("Release 10", 849),
                new VersionParam("Release 10", 849),
                new VersionParam("Release 10 update 1", 141),
                new VersionParam("Release 10 update 15", 849),
                new VersionParam("Release 12", 849),
                new VersionParam("Release 13", 849),
                new VersionParam("Release 14", 849),
                new VersionParam("Release 15", 849),
                new VersionParam("Release 15.1", 849),
                new VersionParam("Release 16.0", 849),
                new VersionParam("Release 17.0", 849),
                new VersionParam("Release 17.0", 849),
                new VersionParam("Release 17.1", 849),
                new VersionParam("Release 17.1", 849)
            };

            public FileVersionInfo(string filename, string cmfilename, string record, Type type, int off) : base(filename, cmfilename, record, type, off)
            {
                Count = 0;
                Versions = new List<Version>();
                VP = new VersionParam();
            }

            public override bool ProcessLine(string line)
            {
                string[] words;
                words = line.Split('|');
                string[] chars;
                string str = "";
                string ver = "";
                string fwd = "";
                string bck = "";

                foreach (var s in words)
                {
                    if (s.IndexOf('=') > 0)
                    {
                        string[] par = s.Split('=');
                        if (par[0] == "COUNT")
                        {
                            str = $"Count={par[1]}";
                            Count = Convert.ToInt16(par[1]);
                        }
                        else
                        {
                            chars = par[1].Split(',');
                            str = par[0] + "=";
                            foreach (var c in chars)
                            {
                                if (c != "")
                                {
                                    int ch = Convert.ToInt16(c);
                                    str += Convert.ToChar(ch);
                                }
                            }
                        }
                        if (str.Contains("VER"))
                            ver = str;
                        if (str.Contains("FWD"))
                            fwd = str;
                        if (str.Contains("BKM"))
                        {
                            bck = str;
                            Version Version = new Version(ver, fwd, bck);
                            Versions.Add(Version);
                            //OutputString(Version.ToString());
                        }
                    }
                }
                base.ProcessLine();
                return true;
            }

            public override void FinishOff()
            {
                foreach (var Ver in VersionParamL)
                {
                    if (Versions[Versions.Count - 1].VER == Ver.Version)
                    {
                        VP = Ver;
                        break;
                    }
                }
                OutputString($"Board last saved by \"Altium {Versions[Versions.Count - 1].VER}\"");
                if (Versions[Versions.Count - 1].VER.Contains("Release"))
                    Post09 = true;
            }

            public int GetPadLength()
            {
                return 0;
            }
        }
    }
}
