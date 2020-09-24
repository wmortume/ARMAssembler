using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ARMAssembler
{
    class Program
    {
        static void Main(string[] args) //Only supports negative numbers on branches
        {            
            List<string> tokens = File.ReadAllLines(Path.GetFullPath("expression.asm")).Where(i => !string.IsNullOrEmpty(i) && !i.StartsWith("@")).ToList();
            ParseOperation(tokens);
        }

        private static void SaveBinaryToBytes(string bin)
        {
            int numOfBytes = bin.Length / 8;
            byte[] bytes = new byte[numOfBytes];
            for (int i = 0; i < numOfBytes; ++i)
            {
                bytes[i] = Convert.ToByte(bin.Substring(8 * i, 8), 2);
            }

            File.WriteAllBytes(Path.GetFullPath("kernel7.img"), bytes);
        }

        private static void ParseOperation(List<string> tokens)
        {
            try
            {
                string parsed = "";
                foreach (string token in tokens)
                {
                    if (token.Length > 2 && token.Substring(0, 2) == "BX")
                    {
                        parsed += ParseBranchExchange(token);
                    }
                    else if (token.Length > 1 && token[0] == 'B')
                    {
                        parsed += ParseBranch(token);
                    }
                    else if (token.Length > 3 && (token.Substring(0, 3) == "LDR" || token.Substring(0, 3) == "STR"))
                    {
                        parsed += ParseLoadStore(token);
                    }
                    else if (token.Length > 3 && token.Substring(0, 3) == "MUL")
                    {
                        parsed += ParseMultiplication(token);
                    }
                    else if (token.Length > 4 && (token.Substring(0, 4) == "MOVW" || token.Substring(0, 4) == "MOVT"))
                    {
                        parsed += ParseMOVTMOVW(token);
                    }
                    else
                    {
                        parsed += ParseDataProcessing(token);
                    }
                }

                SaveBinaryToBytes(parsed);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Couldn't write file: {ex.Message}");
            }
        }

        private static string ParseBranchExchange(string exchange)
        {
            string parsed = "";
            string rn = exchange.Split(' ')[1].TrimStart('R');
            List<string> Specifications = new List<string>();
            ParseSpecsAndCond(exchange, ref parsed, ref Specifications);

            parsed += "0001";
            parsed += "0010";
            parsed += "1111";
            parsed += "1111";
            parsed += "1111";
            parsed += "0001";

            parsed += Convert.ToString(int.Parse(rn), 2).PadLeft(4, '0');

            return ReversedBinary(parsed);
        }

        private static string ParseBranch(string branch)
        {
            string parsed = "";
            string offset;
            List<string> Specifications = new List<string>();
            ParseSpecsAndCond(branch, ref parsed, ref Specifications);

            parsed += "101";

            if (Specifications.Contains("l"))
            {
                parsed += "1";
            }
            else
            {
                parsed += "0";
            }

            if (branch.Split(' ').Length == 2)
            {
                offset = branch.Split(' ')[1];
            }
            else
            {
                offset = branch.Split(' ')[2];
            }

            if (offset.StartsWith("0x")) //convert number from hex to int to string
            {
                offset = Convert.ToInt32(offset, 16).ToString();
            }

            parsed += Convert.ToString(int.Parse(offset) & 0xffffff, 2).PadLeft(24, '0');  //convert number to binary with 24 places and bit masked for negatives

            return ReversedBinary(parsed);
        }

        private static string ParseMultiplication(string mul)
        {
            string parsed = "";
            Regex regex = new Regex(@"^(MUL) (R[\d]+), (R[\d]+), (R[\d]+)$");
            Match match = regex.Match(mul);

            if (match.Success)
            {                
                string rd = match.Groups[2].Value.TrimStart('R');
                string rs = match.Groups[3].Value.TrimStart('R');
                string rm = match.Groups[4].Value.TrimStart('R');

                List<string> Specifications = new List<string>();
                ParseSpecsAndCond(mul, ref parsed, ref Specifications);

                parsed += "000000";
                parsed += "0";
                parsed += "0";
                parsed += Convert.ToString(int.Parse(rd), 2).PadLeft(4, '0');
                parsed += "0000";
                parsed += Convert.ToString(int.Parse(rs), 2).PadLeft(4, '0');
                parsed += "1001";
                parsed += Convert.ToString(int.Parse(rm), 2).PadLeft(4, '0');
            }
            return ReversedBinary(parsed);

        }
        private static string ParseMOVTMOVW(string mov)
        {
            string parsed = "";
            Regex regex = new Regex(@"^(MOVW|MOVT) (R[\d]+), (0x[\da-fA-F]+|[\d]+)$");
            Match match = regex.Match(mov);

            if (match.Success)
            {
                string instr = match.Groups[1].Value;
                string rd = match.Groups[2].Value.TrimStart('R');
                string imm16 = match.Groups[3].Value;

                if (imm16.StartsWith("0x"))
                {
                    imm16 = Convert.ToInt32(imm16, 16).ToString();
                }

                imm16 = Convert.ToString(int.Parse(imm16), 2).PadLeft(16, '0');
                string imm4 = imm16.Substring(0, 4);
                string imm12 = imm16.Substring(4);

                List<string> Specifications = new List<string>();
                ParseSpecsAndCond(mov, ref parsed, ref Specifications);

                parsed += "0011";

                if (instr == "MOVW")
                {
                    parsed += "0000";
                }
                else
                {
                    parsed += "0100";
                }

                parsed += imm4;
                parsed += Convert.ToString(int.Parse(rd), 2).PadLeft(4, '0');
                parsed += imm12;
            }
            return ReversedBinary(parsed);
        }

        private static string ParseLoadStore(string ldr)
        {
            string parsed = "";
            Regex regex = new Regex(@"^(LDR|STR).+(R[\d]+), (\(R[\d]+\))");
            Match match = regex.Match(ldr);

            if (match.Success)
            {
                string instr = match.Groups[1].Value;
                string rd = match.Groups[2].Value.TrimStart('R');
                string rn = match.Groups[3].Value.TrimStart('(', 'R').TrimEnd(')');
                string op2 = "0";

                if (ldr.Split(new[] { ", " }, StringSplitOptions.None).Length == 3)
                {
                    op2 = ldr.Split(new[] { ", " }, StringSplitOptions.None)[2];

                    if (op2.StartsWith("0x"))
                    {
                        op2 = Convert.ToInt32(op2, 16).ToString();
                    }
                }

                List<string> Specifications = new List<string>();
                ParseSpecsAndCond(ldr, ref parsed, ref Specifications);

                parsed += "01";

                if (op2.StartsWith("R"))
                {
                    parsed += "1";
                }
                else
                {
                    parsed += "0";
                }

                if (Specifications.Contains("p"))
                {
                    parsed += "0";
                }
                else
                {
                    parsed += "1";
                }

                if (Specifications.Contains("d"))
                {
                    parsed += "0";
                }
                else
                {
                    parsed += "1";
                }

                if (Specifications.Contains("b"))
                {
                    parsed += "1";
                }
                else
                {
                    parsed += "0";
                }

                if (Specifications.Contains("w"))
                {
                    parsed += "1";
                }
                else
                {
                    parsed += "0";
                }

                if (instr == "LDR")
                {
                    parsed += "1";
                }
                else
                {
                    parsed += "0";
                }

                parsed += Convert.ToString(int.Parse(rn), 2).PadLeft(4, '0');
                parsed += Convert.ToString(int.Parse(rd), 2).PadLeft(4, '0');

                if (op2.StartsWith("R"))
                {
                    parsed += "00000000";
                    parsed += Convert.ToString(int.Parse(op2.TrimStart('R')), 2).PadLeft(4, '0');
                }
                else
                {
                    parsed += Convert.ToString(int.Parse(op2), 2).PadLeft(12, '0');
                }
            }

            return ReversedBinary(parsed);
        }

        private static string ParseDataProcessing(string dataProc)
        {
            string parsed = "";
            Regex regex = new Regex(@"^([A-Z]{3}).+(R[\d]+), (R[\d]+), (0x[\da-fA-F]+|[\d]+|R[\d]+)$");
            Match match = regex.Match(dataProc);

            if (match.Success)
            {
                string instr = match.Groups[1].Value;
                string rd = match.Groups[2].Value.TrimStart('R');
                string rn = match.Groups[3].Value.TrimStart('R');
                string op2 = match.Groups[4].Value;

                if (op2.StartsWith("0x"))
                {
                    op2 = Convert.ToInt32(op2, 16).ToString();
                }

                List<string> Specifications = new List<string>();
                ParseSpecsAndCond(dataProc, ref parsed, ref Specifications);

                parsed += "00";

                if (op2.StartsWith("R"))
                {
                    parsed += "0";
                }
                else
                {
                    parsed += "1";
                }

                if (instr == "AND")
                {
                    parsed += "0000";
                }
                else if (instr == "ORR")
                {
                    parsed += "1100";
                }
                else if (instr == "ADD")
                {
                    parsed += "0100";
                }
                else if (instr == "SUB")
                {
                    parsed += "0010";
                }

                if (Specifications.Contains("s"))
                {
                    parsed += "1";
                }
                else
                {
                    parsed += "0";
                }

                parsed += Convert.ToString(int.Parse(rn), 2).PadLeft(4, '0');
                parsed += Convert.ToString(int.Parse(rd), 2).PadLeft(4, '0');

                if (op2.StartsWith("R"))
                {
                    parsed += "00000000";
                    parsed += Convert.ToString(int.Parse(op2.TrimStart('R')), 2).PadLeft(4, '0');
                }
                else
                {
                    parsed += "0000";
                    parsed += Convert.ToString(int.Parse(op2), 2).PadLeft(8, '0');
                }
            }

            return ReversedBinary(parsed);
        }

        private static string ParseCondition(string cond)
        {
            if (cond.Length == 3 || cond.Length == 4)
            {
                if (cond.Contains("EQ"))
                {
                    return "0000";
                }
                else if (cond.Contains("NE"))
                {
                    return "0001";
                }
                else if (cond.Contains("LT"))
                {
                    return "1011";
                }
                else if (cond.Contains("LE"))
                {
                    return "1101";
                }
                else if (cond.Contains("GT"))
                {
                    return "1100";
                }
                else if (cond.Contains("GE"))
                {
                    return "1010";
                }
                else
                {
                    return "1110";
                }
            }
            else
            {
                return "1110";
            }
        }

        private static void ParseSpecsAndCond(string dataProc, ref string parsed, ref List<string> Specifications)
        {
            if (dataProc.Split(' ').Length >= 2 && dataProc.Split(' ')[1].StartsWith("{"))
            {
                string slittedSpecificiations = dataProc.Split(' ')[1].TrimStart('{').TrimEnd('}');
                Specifications = slittedSpecificiations.Split(',').ToList();
                parsed += ParseCondition(dataProc.Split(' ')[0]);
            }
            else if (dataProc.Split(' ').Length >= 3 && dataProc.Split(' ')[2].StartsWith("{"))
            {
                string slittedSpecificiations = dataProc.Split(' ')[2].TrimStart('{').TrimEnd('}');
                Specifications = slittedSpecificiations.Split(',').ToList();
                parsed += ParseCondition(dataProc.Split(' ')[0]);
            }
            else
            {
                parsed += ParseCondition(dataProc.Split(' ')[0]);
            }
        }

        private static string ReversedBinary(string bin)
        {
            IEnumerable<string> splitAndReverse = Enumerable.Range(0, bin.Length / 8)
                    .Select(i => bin.Substring(i * 8, 8)).Reverse();

            return string.Join("", splitAndReverse);
        }
    }
}
