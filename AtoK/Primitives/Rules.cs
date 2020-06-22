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

        class RuleKind : Object
        {
            public string Kind { get; set; }
            public List<Rule> Rules;

            public RuleKind(Rule R)
            {
                // create new rulekind node
                Rules = new List<Rule>();
                if (R != null)
                {
                    Add(R);
                    Kind = R.RuleKind;
                }
            }

            public void Add(Rule R)
            {
                // add rule into rule list based on priority
                for(var i=0; i<Rules.Count; i++)
                {
                    if(R.Priority < Rules[i].Priority)
                    {
                        Rules.Insert(i, R);
                        return;
                    }
                }
                // empty list or lowest priority so just add
                Rules.Add(R);
            }

            public override string ToString()
            {
                var s = new StringBuilder("");
                foreach (var Rule in Rules)
                {
                    s.Append($"    Priority={Rule.Priority}  {Rule.ToString()}\n");
                }
                return s.ToString();
            }
        }

        // class for design rule objects
        class Rule : PCBObject
        {
            private readonly string Line;
            public string Name { get; set; }
            public string RuleKind { get; set; }
            public string NetScope { get; set; }
            public string LayerKind { get; set; }
            public bool   Enabled { get; set; }
            public int    Priority { get; set; }
            public string Comment { get; set; }
            public double Value { get; set; }
            public double ValueMin { get; set; }
            public double ValueMax { get; set; }
            public bool   Valid;
            public string Scope1Expression { get; set; }
            public string Scope2Expression { get; set; }

            private Rule()
            {
            }

            public Rule(string line)
            {
                string param;
                string[] words = line.Split('|');
                Line = line;
                Enabled = false;
                if ((param = GetString(line, "|ENABLED=")) != "")
                {
                    Enabled = param == "TRUE";
                }
                if (!Enabled)
                    return;
                Name      = GetString(line, "|NAME=");
                RuleKind  = GetString(line, "|RULEKIND=");
                NetScope  = GetString(line, "|NETSCOPE=");
                Comment   = GetString(line, "|COMMENT=");
                LayerKind = GetString(line, "|LAYERKIND=");
                Priority  = Convert.ToInt16(GetString(line, "PRIORITY="));
                Value = 0;
                Valid = false;
                Scope1Expression = GetString(line, "SCOPE1EXPRESSION=");
                Scope2Expression = GetString(line, "SCOPE2EXPRESSION=");
                //OutputString($"{RuleKind} {Name} 1={Scope1Expression} 2={Scope2Expression}");

                switch (RuleKind)
                {
                    case "Clearance":
                        {
                            Value = GetNumberInMM(GetString(line, "GAP="));
                            Valid = true;
                            Rules.Clearance.Add(this);
                        }
                        break;
                    case "PasteMaskExpansion":
                        {
                            Value = GetNumberInMM(GetString(line, "EXPANSION="));
                            Valid = true;
                            Rules.PasteMaskExpansion.Add(this);
                        }
                        break;
                    case "SolderMaskExpansion":
                        {
                            Value = GetNumberInMM(GetString(line, "EXPANSION="));
                            Valid = true;
                            Rules.SolderMaskExpansion.Add(this);
                        }
                        break;
                    case "PlaneClearance":
                        {
                            Value = GetNumberInMM(GetString(line, "CLEARANCE="));
                            Valid = true;
                            Rules.PlaneClearance.Add(this);
                        }
                        break;
                    case "PolygonConnect":
                        {
                            Value = GetNumberInMM(GetString(line, "RELIEFCONDUCTORWIDTH="));
                            Valid = true;
                            Rules.PolygonConnect.Add(this);
                        }
                        break;
                    case "MinimumSolderMaskSliver":
                    /*    {
                            Value = GetNumberInMM(GetString(line, "MINSOLDERMASKWIDTH="));
                            Valid = true;
                        }*/
                        break;
                    case "HoleSize":
                    /*    {
                            ValueMin = GetNumberInMM(GetString(line, "MINLIMIT="));
                            Valid = true;
                            ValueMax = GetNumberInMM(GetString(line, "MAXLIMIT="));
                            Valid = true;
                        }*/
                        break;
                    // following rules not of interest yet
                    case "ComponentClearance":          break;
                    case "Width":                       break;
                    case "MinimumAnnularRing":          break;
                    case "SMDToCorner":                 break;
                    case "RoutingVias":                 break;
                    case "ShortCircuit":                break;
                    case "DiffPairsRouting":            break;
                    case "NetAntennae":                 break;
                    case "SilkscreenOverComponentPads": break;
                    case "HoleToHoleClearance":         break;
                    case "FanoutControl":               break;
                    case "LayerPairs":                  break;
                    case "Height":                      break;
                    case "Testpoint":                   break;
                    case "TestPointUsage":              break;
                    case "RoutingTopology":             break;
                    case "RoutingCorners":              break;
                    case "RoutingLayers":               break;
                    case "RoutingPriority":             break;
                    case "PlaneConnect":                break;
                    case "UnRoutedNet":                 break;
                    default:                            break;
                }
            }

            public override string ToString()
            {
                return $"Name={Name}\n        1={Scope1Expression}\n        2={Scope2Expression}\n        Value={Value}";
            }
        }

        static bool Evaluate(string rule)
        {
            return rule == "All";
        }

        // get a design rule value e.g. polygon clearance
        static double GetRuleValue(PCBObject Obj, string kind, string name)
        {
            var P = new Parser();

            foreach(var RuleKind in RuleKindsL)
            {
                if(RuleKind.Kind == kind)
                {
                    foreach(var Rule in RuleKind.Rules)
                    {
                        if (P.Eval(Obj, P.ParseAltiumExpr(Rule.Scope1Expression)) && P.Eval(Obj, P.ParseAltiumExpr(Rule.Scope2Expression)))
                        {
                            return Rule.Value;
                        }
                    }
                }
            }
            // not found return default value TODO find out what proper default value is
            return 0.1; 
            /*
            foreach (var Rule in RulesL)
            {
                if ((Rule.RuleKind == kind) && (Rule.Name == name))
                {
                    return Rule.Value;
                }
            }
            */
            return 0;
        }


        // class for the rules document entry in the pcbdoc file
        class Rules : PcbDocEntry
        {
            public static RuleKind SolderMaskExpansion;
            public static RuleKind PasteMaskExpansion;
            public static RuleKind PlaneClearance;
            public static RuleKind PolygonConnect;
            public static RuleKind Clearance;


            public Rules(string filename, string cmfilename, string record, Type type, int offset) : base(filename, cmfilename, record, type, offset)
            {
                SolderMaskExpansion = new RuleKind(null);
                PasteMaskExpansion  = new RuleKind(null);
                PlaneClearance      = new RuleKind(null);
                PolygonConnect      = new RuleKind(null);
                Clearance           = new RuleKind(null);
            }

            public override bool ProcessLine(string line)
            {
                base.ProcessLine();
                try
                {
                    Rule Rule = new Rule(line);
                    if (Rule.Enabled && Rule.Valid)
                    {
                        RulesL.Add(Rule);
                        bool Found = false;
                        foreach (var RuleKind in RuleKindsL)
                        {
                            if (RuleKind.Kind == Rule.RuleKind)
                            {
                                // rulekind already exists so add to rule list
                                RuleKind.Add(Rule);
                                Found = true;
                            }
                        }
                        if (!Found)
                        {
                            RuleKind RuleK = new RuleKind(Rule);
                            RuleKindsL.Add(RuleK);
                        }
                    }
                }
                catch (Exception Ex)
                {
                    CheckThreadAbort(Ex);
                }
                return true;
            }

            public override void FinishOff()
            {
                /*
                */
                OutputString($"Clearance\n{Clearance.ToString()}");
                OutputString($"SolderMaskExpansion\n{SolderMaskExpansion.ToString()}");
                OutputString($"PasteMaskExpansion\n{PasteMaskExpansion.ToString()}");
                OutputString($"PlaneClearance\n{PlaneClearance.ToString()}");
                OutputString($"PolygonConnect\n{PolygonConnect.ToString()}");
                base.FinishOff();
            }
        }
    }
}
