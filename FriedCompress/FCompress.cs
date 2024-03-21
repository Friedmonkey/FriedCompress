using FHex;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace FriedCompress
{
    public static class FCompress
    {
        public static string Compress(string Input,bool includeTree = true) => new fHex(Base64Compress(Input,includeTree)).ToString();
        public static string Decompress(string Input) => Base64Decompress(new fHex(Input).GetBytes());

        public static byte[] Compress(byte[] Input, bool includeTree = true) => Base64Compress(new fHex(Input).ToString(), includeTree);
        public static byte[] Decompress(byte[] Input) => new fHex(Base64Decompress(Input)).GetBytes();


        public static byte[] Base64Compress(string Base64String,bool UseTree = true)
        {
            var base64Chars = Split(Base64String, 1).ToList();

            if (base64Chars.Count == 0)
                return new byte[] { };

            FCNode Tree = new FCNode();
            if (UseTree)
                Tree = GenerateTree(base64Chars);
            else
                Tree = null; //use baked in tree later which has every char

            PrintJson(new List<FCNode> { Tree });

            List<fBit> Paths = new List<fBit>();

            Console.WriteLine("Comp" + "\t" + "Binary rep" + "\t" + "char" + "\t" + "frequency");
            foreach (string Char in base64Chars)
            {
                var LookedUpNode = CharLookup(Char,Tree,out fBit BackTracedPath);
                if (LookedUpNode != null)
                {
                    var Path = BackTracedPath.Reversed();
                    //because its back traced

                    Console.WriteLine(Path.ToString() + "\t" + new fHex(Char).ToBinaryStringNew() + "\t" + Char + "\t" + LookedUpNode.Fequency);
                    Paths.Add(Path);
                }
            }

            fBit TreeBits = new fBit(); //= ConvertTreeToBitArray(Tree);
            TreeBits.FromBoolArray(TreeConverter.ConvertTreeToBitArray(Tree).ToArray());
            Console.WriteLine("TreeBits:"+TreeBits.ToString());

            //fBit SplitSig = new fBit("#@!");
            //Console.WriteLine($"SplitSig = {SplitSig}");

            fBit fullPath = new fBit();
            foreach (fBit path in Paths)
            {
                fullPath.AddBits(path);
            }
            Console.WriteLine("FullPath:"+fullPath.ToString());

            int amountOfUseTreeBoolBits = 1;
            int amountOfTreePositionBits1 = 8;
            int amountOfTreePositionBits2 = 8;
            int amountOfExtraBitsBits = 3;

            fBit Final = new fBit();
            Final.AddBit(UseTree); //first bit indicates weather or not a tree is used
            //add 8 bits or more which will be used as a number which will tell how long the tree is/ how many bits to skip
            //add 5,6 or8 bits which will be used as a number that tells us how many bits we need to omit/delet from the end because a byte has to be 8 bits so if we have 9 bits 7 will be added to make it be able to turn into a byte so we note the number 7 (00000111) at the beginning telling us to remove/ignore the last 7 bits, we only actually need 3 bits now that i think about it because 7 is all we need which is (111)

            Final.AddBits(amountOfTreePositionBits1,false);
            Final.AddBits(amountOfTreePositionBits2,false);

            Final.AddBits(amountOfExtraBitsBits,false);

            if (UseTree)
            {
                Final.AddBits(TreeBits);
                int previousBits = amountOfUseTreeBoolBits;
                var bits1 = fBit.GetTrimmedBits((int)Math.Floor(Final.Length/2d),amountOfTreePositionBits1);
                for (int i = 0; i < amountOfTreePositionBits1; i++)
                {
                    Final.SetBit(i+previousBits, bits1[i]);
                }
                previousBits += amountOfTreePositionBits1;
                var bits2 = fBit.GetTrimmedBits((int)Math.Ceiling(Final.Length / 2d), amountOfTreePositionBits2);
                for (int i = 0; i < amountOfTreePositionBits2; i++)
                {
                    Final.SetBit(i + previousBits, bits2[i]);
                }
            }
            Final.AddBits(fullPath);


            string input = Final.ToString();
            int extraEndingBits = 0;
            // Pad the input with zeros to make its length a multiple of 8
            while (input.Length % 8 != 0)
            {
                input += "0";
                Final.AddBit(false); //add dummy bit at end to make it multiple of 8
                extraEndingBits++;
            }

            if (extraEndingBits != 0)
            {

                int previousBits = amountOfUseTreeBoolBits + amountOfTreePositionBits1 + amountOfTreePositionBits2;
                var bits = fBit.GetTrimmedBits(extraEndingBits,amountOfExtraBitsBits);
                for (int i = 0; i < amountOfExtraBitsBits; i++)
                {
                    Final.SetBit(i + previousBits, bits[i]);
                }
                input = Final.ToString();
            }

            int numBytes = input.Length / 8;
            byte[] bytes = new byte[numBytes];
            for (int i = 0; i < numBytes; ++i)
            {
                bytes[i] = Convert.ToByte(input.Substring(8 * i, 8), 2);
            }


            //compress using this dumbass tree
            return bytes;
        }

        public static string Base64Decompress(byte[] CompressedBytes) 
        {
            string binaryString = new fHex(CompressedBytes).ToBinaryStringNew();
            fBit CompressedBits = fBit.FromBinaryString(binaryString);

            //layout:
            //[useTreeBool(1)][treePosition(8)][extraEndingBitsCount(3)]
            int amountOfUseTreeBoolBits = 1;
            int amountOfTreePositionBits1 = 8;
            int amountOfTreePositionBits2 = 8;
            int amountOfextraEndingBitsCount = 3;
            #region GetLayoutHeader

            int currentIndex = 0;

            int amount = amountOfUseTreeBoolBits;
            var useTreeBool = CompressedBits.GetBetween(currentIndex, currentIndex + amount);
            currentIndex += amount;

            amount = amountOfTreePositionBits1;
            var treePosition1 = CompressedBits.GetBetween(currentIndex, currentIndex + amount);
            currentIndex += amount;

            amount = amountOfTreePositionBits2;
            var treePosition2 = CompressedBits.GetBetween(currentIndex, currentIndex + amount);
            currentIndex += amount;

            amount = amountOfextraEndingBitsCount;
            var extraEndingBitsCount = CompressedBits.GetBetween(currentIndex, currentIndex + amount);
            currentIndex += amount;
            #endregion

            bool useTree = useTreeBool.ToBoolArray().First();

            int treePos1 = fBit.FromTrimmedBits(treePosition1.ToBoolArray(),amountOfTreePositionBits1);
            int treePos2 = fBit.FromTrimmedBits(treePosition2.ToBoolArray(),amountOfTreePositionBits2);
            int extraBitsCount = fBit.FromTrimmedBits(extraEndingBitsCount.ToBoolArray(),amountOfextraEndingBitsCount);
            int treePos = treePos1 + treePos2;

            FCNode Tree = new FCNode();

            if (useTree)
            { 
                var TreeBits = CompressedBits.GetBetween(currentIndex, treePos);
                //Tree = ConvertBitArrayToTree(TreeBits);
                Tree = TreeConverter.ConvertBitArrayToTree(TreeBits.ToBoolArray().ToList());
            }

            fBit Path = CompressedBits.GetBetween(treePos,CompressedBits.Length-extraBitsCount);

            var Turns = fBit.Split(Path.ToString(), 1).ToList();
            string endResult = "";
            while (Turns.Count != 0)
            {
                var LookedUpBit = BitLookup(Turns,Tree,out string Text);
                if (LookedUpBit != null)
                {
                    endResult += Text;
                }
            }
            return endResult;
        }

        private static FCNode GenerateTree(List<string> base64Chars) 
        {
            List<FCChar> Chars = new List<FCChar>();

            foreach (var Char in base64Chars) 
            {
                FCChar ExistingChar = Chars.FirstOrDefault(c => c.Char == Char);
                if (ExistingChar == null)
                {
                    FCChar tmpChar = new FCChar();
                    tmpChar.Char = Char;
                    tmpChar.Fequency = 1;
                    Chars.Add(tmpChar);
                }
                else
                {
                    ExistingChar.Fequency++;
                }

            }
            List<FCNode> Nodes = new List<FCNode>(Chars);
            //PrintJson(Nodes);
            for (int i = 0; i < Chars.Count; i++)
            {
                if (Nodes.Count == 1)
                {
                    if (Nodes.First() is FCChar fChar)
                    {
                        FCNode repNode = new FCNode();
                        repNode.Fequency = fChar.Fequency;
                        repNode.LeftNode = fChar;
                        repNode.RightNode = null;

                        Nodes[0] = repNode;
                    }
                    break;
                }
                Nodes.Sort((one, two) => two.Fequency - one.Fequency);
                //PrintJson(Nodes);

                var left = Nodes.Last();
                Nodes.Remove(left);
                var right = Nodes.Last();
                Nodes.Remove(right);

                //PrintJson(Nodes);

                FCNode newNode = new FCNode();
                newNode.Fequency = left.Fequency+right.Fequency;
                newNode.LeftNode = left;
                newNode.RightNode = right;

                Nodes.Add(newNode);

                //PrintJson(Nodes);
            }
            //PrintJson(Nodes);
            //frequency bullshit here
            return Nodes.First();

        }

        private static FCNode CharLookup(string Char, FCNode Tree,out fBit BackTracedPath) 
        {
            BackTracedPath = new fBit();
            if (Tree == null || Tree is FCChar fCChar && fCChar.Char == Char)
            {
                return Tree;
            }

            FCNode leftResult = CharLookup(Char,Tree.LeftNode,out BackTracedPath);
            if (leftResult != null)
            {
                BackTracedPath.AddBit(false);
                return leftResult;
            }

            FCNode rightResult = CharLookup(Char,Tree.RightNode, out BackTracedPath);
            if (rightResult != null)
            {
                BackTracedPath.AddBit(true);
                return rightResult;
            }
            return null;
        }
        public static FCNode BitLookup(List<string> Turns, FCNode Tree,out string Text) 
        {
            Text = "";
            if (Tree == null || Tree is FCChar fCChar)
            {
                var vchar = (Tree as FCChar);
                if (vchar != null)
                    Text += vchar.Char;
                return Tree;
            }
            var Turn = Turns.First();
            Turns.Remove(Turn);

            if (Turn == "0") //left
            {
                var leftResult = BitLookup(Turns,Tree.LeftNode,out Text);
                //if (leftResult != null) 
                //{
                //    Text += (leftResult as FCChar).Char;
                return leftResult;
                //}
            }
            else if (Turn == "1") //right
            {
                var rightResult = BitLookup(Turns, Tree.RightNode, out Text);
                //if (rightResult != null)
                //{
                //    Text += (rightResult as FCChar).Char;
                return rightResult;
                //}
            }
            else
            {
                throw new Exception($@"incorrect bit! (expected either a 1 or a 0, got ""{Turn}"")");
            }
            return null;
        }




        private static IEnumerable<string> Split(string str, int chunkSize)
        {
            return Enumerable.Range(0, str.Length / chunkSize)
                .Select(i => str.Substring(i * chunkSize, chunkSize));
        }

        private static void PrintJson(List<FCNode> nodes,bool write = true) 
        {
            string strNodes = JsonConvert.SerializeObject(nodes);
            //Console.WriteLine(strNodes);

            if (write) 
            {
                File.WriteAllText("json.json",strNodes);
            }
        }
    }
    public class FCChar : FCNode 
    {
        public string Char { get; set; }
    }
    public class FCNode 
    {
        public FCNode LeftNode { get; set; }
        public FCNode RightNode { get; set; }
        public int Fequency { get; set; }
    }
    public static class TreeConverter
    {
        public static List<bool> ConvertTreeToBitArray(FCNode root)
        {
            List<bool> bitArray = new List<bool>();
            ConvertNodeToBitArray(root, bitArray);
            return bitArray;
        }

        private static void ConvertNodeToBitArray(FCNode node, List<bool> bitArray)
        {
            if (node == null)
                return;

            // Add a 'true' bit to indicate the presence of a node
            bitArray.Add(true);

            if (node is FCChar charNode)
            {
                // Add a 'true' bit to indicate a character node
                bitArray.Add(true);

                // Add the character's bits to the array (assuming ASCII characters)
                foreach (char c in charNode.Char)
                {
                    for (int i = 7; i >= 0; i--)
                    {
                        bitArray.Add((c & (1 << i)) != 0);
                    }
                }
            }
            else
            {
                // Add a 'false' bit to indicate an internal node
                bitArray.Add(false);

                // Recursively convert left and right nodes
                ConvertNodeToBitArray(node.LeftNode, bitArray);
                ConvertNodeToBitArray(node.RightNode, bitArray);
            }
        }

        public static FCNode ConvertBitArrayToTree(List<bool> bitArray)
        {
            int index = 0;
            return ConvertBitArrayToNode(bitArray, ref index);
        }

        private static FCNode ConvertBitArrayToNode(List<bool> bitArray, ref int index)
        {
            if (index >= bitArray.Count)
                return null;

            bool hasNode = bitArray[index++];
            if (!hasNode)
                return null;

            bool isCharNode = bitArray[index++];
            if (isCharNode)
            {
                StringBuilder charBuilder = new StringBuilder();
                //for (int i = 0; i < 8; i++)
                {
                    byte b = 0;
                    for (int j = 7; j >= 0; j--)
                    {
                        //i feel like it has to do with 8 bits = 256 numbers
                        //but my tree bits are 250 + maby some other not entirely sure

                        //yeah def
                        //1 bit for use tree bool
                        //8 bits for tree location (256)
                        //3 bits for extra bits count (7)
                        //so 250 + 1 + 3 = 254
                        //254 + 8 = 262
                        //262 is the tree location which is larger then 256 so it cant handle, you gotta fix this!
                        bool bit = bitArray[index++];
                        if (bit)
                            b |= (byte)(1 << j);
                    }
                    charBuilder.Append((char)b);
                }

                return new FCChar { Char = charBuilder.ToString() };
            }
            else
            {
                FCNode node = new FCNode();
                node.LeftNode = ConvertBitArrayToNode(bitArray, ref index);
                node.RightNode = ConvertBitArrayToNode(bitArray, ref index);
                return node;
            }
        }
    }
}
