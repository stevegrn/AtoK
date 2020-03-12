using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ConvertToKicad
{
    public partial class ConvertPCBDoc
    {
        // class for scope objects used in design rules
        class Scope : Object
        {
            public string Expression { get; set; }
            public string Kind { get; set; }
            public string Value { get; set; }
            public double Gap { get; set; }

            Scope()
            {
            }

            public Scope(int n, string rule)
            {
                Kind = GetString(rule, $"SCOPE{n}_0_KIND=");
                Expression = GetString(rule, $"SCOPE{n}EXPRESSION=");
                Value = GetString(rule, $"SCOPE{n}_0_VALUE=");
                Gap = GetNumberInMM(GetString(rule, "|GAP="));
            }

            public override string ToString()
            {
                return $"Scope={Kind} Expression={Expression} Value={Value} Gap={Gap}";
            }
        }

        // class for design rule objects
        class Rule : Object
        {
            private readonly string Line;
            public string Name { get; set; }
            public string RuleKind { get; set; }
            public string NetScope { get; set; }
            public string LayerKind { get; set; }
            public bool Enabled { get; set; }
            public int Priority { get; set; }
            public string Comment { get; set; }
            public Scope Scope1 { get; set; }
            public Scope Scope2 { get; set; }
            public double Value { get; set; }
            public double ValueMin { get; set; }
            public double ValueMax { get; set; }
            public bool Valid;

            private Rule()
            {
            }

            public Rule(string line)
            {
                string param;

                Line = line;
                if ((param = GetString(line, "|ENABLED=")) != "")
                {
                    Enabled = param == "TRUE";
                }
                Name = GetString(line, "|NAME=");
                RuleKind = GetString(line, "|RULEKIND=");
                NetScope = GetString(line, "|NETSCOPE=");
                Comment = GetString(line, "|COMMENT=");
                LayerKind = GetString(line, "|LAYERKIND=");
                Priority = Convert.ToInt16(GetString(line, "PRIORITY="));
                Scope1 = new Scope(1, line);
                //                OutputError(Name + " " + RuleKind + " " + Scope1.ToString());
                Scope2 = new Scope(2, line);
                //                OutputError(Name+" "+RuleKind + " " + Scope2.ToString());
                Value = 0;
                Valid = false;

                switch (RuleKind)
                {
                    case "Clearance":
                    case "ComponentClearance":
                    case "PolygonClearance":
                        {
                            Value = GetNumberInMM(GetString(line, "GAP="));
                            Valid = true;
                        }
                        break;
                    case "PasteMaskExpansion":
                    case "SolderMaskExpansion":
                        {
                            Value = GetNumberInMM(GetString(line, "EXPANSION="));
                            Valid = true;
                        }
                        break;
                    case "PlaneClearance":
                        {
                            Value = GetNumberInMM(GetString(line, "CLEARANCE="));
                            Valid = true;
                        }
                        break;
                    case "MinimumSolderMaskSliver":
                        {
                            Value = GetNumberInMM(GetString(line, "MINSOLDERMASKWIDTH="));
                            Valid = true;
                        }
                        break;
                    case "HoleSize":
                        {
                            ValueMin = GetNumberInMM(GetString(line, "MINLIMIT="));
                            Valid = true;
                            ValueMax = GetNumberInMM(GetString(line, "MAXLIMIT="));
                            Valid = true;
                        }
                        break;
                    case "PolygonConnect":
                        {
                            Value = GetNumberInMM(GetString(line, "RELIEFCONDUCTORWIDTH="));
                            Valid = true;
                        }
                        break;
                    case "Width":
                        {
                        }
                        break;
                    case "MinimumAnnularRing":
                        {
                        }
                        break;
                    case "SMDToCorner":
                        {
                        }
                        break;
                    case "RoutingVias":
                        {
                        }
                        break;

                    case "ShortCircuit": break;
                    case "DiffPairsRouting": break;
                    case "NetAntennae": break;
                    case "SilkscreenOverComponentPads": break;
                    case "HoleToHoleClearance": break;
                    case "FanoutControl": break;
                    case "LayerPairs": break;
                    case "Height": break;
                    case "Testpoint": break;
                    case "TestPointUsage": break;
                    case "RoutingTopology": break;
                    case "RoutingCorners": break;
                    case "RoutingLayers": break;
                    case "RoutingPriority": break;
                    case "PlaneConnect": break;
                    case "UnRoutedNet": break;
                    default: break;
                }
            }

            public override string ToString()
            {
                return "";
            }
        }

        // get a design rule value e.g. polygon clearance
        static double GetRuleValue(string kind, string name)
        {
            foreach (var Rule in RulesL)
            {
                if ((Rule.RuleKind == kind) && (Rule.Name == name))
                {
                    return Rule.Value;
                }
            }
            return 0;
        }


        // class for the rules document entry in the pcbdoc file
        class Rules : PcbDocEntry
        {
            public Rules(string filename, string record, Type type, int offset) : base(filename, record, type, offset)
            {
            }

            public override bool ProcessLine(string line)
            {
                base.ProcessLine();
                try
                {
                    Rule Rule = new Rule(line);
                    if (Rule.Enabled && Rule.Valid)
                        RulesL.Add(Rule);
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
