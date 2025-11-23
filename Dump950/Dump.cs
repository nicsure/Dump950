using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dump950
{
    public class Dump
    {
        private static SerialPort? com = null;

        private const int EOS = -1;
        private const int NULL_COM = -2;
        private const int READ_ERROR = -3;

        private const int FLASH_SIZE = 4096;

        private static int GetByte()
        {

            if (com == null) return NULL_COM;
            try { return com.ReadByte(); } catch { return READ_ERROR; }
        }

        public static void Go()
        {
            string[] ports = SerialPort.GetPortNames();

            Console.WriteLine("RT-950 SPI Flash Storage Dump\r\n");
            Console.WriteLine("Available Serial Ports");
            for (int i = 0; i < ports.Length; i++)
                Console.WriteLine($"{i}. {ports[i]}");
            while (true)
            {
                Console.Write($"Enter Selection (0-{ports.Length-1}) or Q to Quit : ");
                string sel = (Console.ReadLine() ?? string.Empty).ToUpper();
                if (sel.StartsWith('Q'))
                    return;
                if (!int.TryParse(sel, out int comNumber))
                    continue;
                try
                {
                    com = new($"{ports[comNumber]}", 115200, Parity.None, 8, StopBits.One)
                    {
                        ReadTimeout = 100000000
                    };
                    com.Open();
                }
                catch  
                {
                    Console.Write($"Cannot open {ports[comNumber]}");
                    com?.Dispose();
                    continue;
                }
                Console.WriteLine($"Listening on {ports[comNumber]}");
                break;
            }
            using (com)
            {
                int blockCount = 0;
                byte[] dump = new byte[FLASH_SIZE * 1024];
                bool[] blocks = new bool[FLASH_SIZE];
                while (true)
                {
                    int sig = GetByte();
                    if (sig < 0)
                    {
                        Console.WriteLine("COM Port Error");
                        return;
                    }
                    if (sig != 0xaa) 
                        continue;
                    if (GetByte() != 0x30) 
                        continue;
                    int addr = GetByte() | (GetByte() << 8) | (GetByte() << 16) | (GetByte() << 24);
                    if (addr < 0 || addr > (FLASH_SIZE - 1) * 1024) 
                        continue;
                    if ((addr & 0x3ff) != 0) 
                        continue;
                    int block = addr / 1024;
                    byte cs1 = 0;
                    for (int i = 0; i < 1024; i++)
                    {
                        dump[addr + i] = (byte)GetByte();
                        cs1 += dump[addr + i];
                    }
                    if (cs1 != GetByte()) 
                        continue;
                    if (!blocks[block])
                    {
                        blockCount++;
                        Console.WriteLine($"Read Block {blockCount}/{blocks.Length}");
                    }
                    blocks[block] = true;
                    if(blockCount >= blocks.Length)
                    {
                        try
                        {
                            File.WriteAllBytes("dump.bin", dump);
                            Console.WriteLine("Dump Finished: 'dump.bin' created.");
                        }
                        catch
                        {
                            Console.WriteLine("Unable to write 'dump.bin', file system error");
                        }
                        Console.ReadLine();
                        return;
                    }
                }
            }
        }
    }
}
