using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FriedCompress;
using FHex;

namespace FriedCompressConsole
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string text = "According to all known laws of aviation, there is no way a bee should be able to fly. Its wings are too small to get its fat little body off the ground. The bee, of course, flies anyway because bees don't care what humans think is impossible.";
            Console.WriteLine(text);


            byte[] comp = FCompress.Base64Compress(text);

            fHex Compressed = new fHex(comp);
            fHex Uncompressed = new fHex(text);

            Console.WriteLine();
            Console.WriteLine($"Compressed ({Compressed.Length}) :");
            Console.WriteLine(Compressed.ToString());
            Console.WriteLine($"binary ({Compressed.ToBinaryStringNew().Length}) :");
            Console.WriteLine(Compressed.ToBinaryStringNew());
            Console.WriteLine("\n");
            Console.WriteLine($"Uncompressed ({Uncompressed.Length}) :");
            Console.WriteLine(Uncompressed.ToString());
            Console.WriteLine($"binary ({Uncompressed.ToBinaryStringNew().Length}) :");
            Console.WriteLine(Uncompressed.ToBinaryStringNew());

            Console.WriteLine("\n");
            Console.WriteLine("\n");
            //Console.WriteLine("Manual:");
            //Console.WriteLine(fHex.FromByteArrayString("8B 15 BC").ToBinaryStringNew());


            string end = FCompress.Base64Decompress(comp);
            Console.WriteLine(end);


            Console.ReadLine();
        }
    }
}
