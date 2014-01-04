using NDesk.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;

namespace TeensyCrypt
{
    enum mode
    {
        enc,
        dec
    }
    class Program
    {
        const int blockSize = 4096;
        static void Main(string[] args)
        {
            bool eArg = false, dArg = false;
            string inputFile = null, outputFile = null, portName = null;

            var p = new OptionSet() {            
                { "i=", "The input file",   v => inputFile = v },
                { "o=", "The output file",   v => outputFile = v },
                { "p=", "Port name",   v => portName = v },
                { "e",  "Encrypt the file", v => eArg = v != null },
                { "d",  "Decrypt the file",  v => dArg = v != null },
            };

            try
            {
                p.Parse(args);
                if (inputFile == null || outputFile == null || portName == null)
                {
                    p.WriteOptionDescriptions(Console.Out);
                    return;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return;
            }

            mode currMode;
            if (eArg)
                currMode = mode.enc;
            else if (dArg)
                currMode = mode.dec;
            else
                return;

            try
            {

                SerialPort port;

                if (File.Exists(outputFile))
                    File.Delete(outputFile);

                port = new SerialPort(portName);
                port.Open();

                port.Write(prepareSwitch(currMode), 0, 32);                

                byte[] fileBytes = File.ReadAllBytes(inputFile);

                FileStream fs = new FileStream(outputFile, FileMode.Append);
                BinaryWriter bw = new BinaryWriter(fs);
                
                //duljina datoteke za dekriptiranje
                int origLen = 0;
                if (currMode == mode.enc)
                {
                    port.Write(BitConverter.GetBytes(fileBytes.Length), 0, 4);
                    //čitanje i zapisivanje header-a za enkriptirani fajl
                    while (port.BytesToRead < 20) { }
                    byte[] encInitBuffer = new byte[20];
                    port.Read(encInitBuffer, 0, 20);
                    bw.Write(encInitBuffer);
                }
                else
                {
                    //daj mu len, iv
                    port.Write(fileBytes, 0, 20);
                    origLen = BitConverter.ToInt32(fileBytes, 0);
                }
                              
                byte[] readBuffer = new byte[blockSize];
                byte[] writeBuffer = new byte[blockSize];

                Console.WriteLine("Working...");
                var watch = Stopwatch.StartNew();  

                for (int i = (currMode == mode.enc) ? 0 : 20; i < fileBytes.Length; i = i + blockSize)
                {
                    if ((fileBytes.Length - i) < blockSize)
                    {
                        Array.Clear(writeBuffer, 0, blockSize);
                        Array.Copy(fileBytes, i, writeBuffer, 0, fileBytes.Length - i);
                        port.Write(writeBuffer, 0, blockSize);
                    }
                    else
                    {
                        port.Write(fileBytes, i, blockSize);
                    }

                    //pročitaj rezultat
                    while (port.BytesToRead < blockSize) { }
                    port.Read(readBuffer, 0, blockSize);
                    //najprije provjerava da li je u dec modu i da li se nalazi na zadnjem bloku - ako da onda reže padding na temelju orig duljine datoteke (origLen)
                    bw.Write(((currMode == mode.dec) && (((i - 20) / blockSize) == (origLen / blockSize))) ? trimPadding(readBuffer, (i + blockSize - 20 - origLen)) : readBuffer);
                }
                watch.Stop();
                Console.WriteLine("Finished in " + watch.ElapsedMilliseconds + " ms");

                port.Close();
                bw.Close();
                fs.Close();
            }
            catch (Exception e)
            {
                string msg = e.Message;
                while (e.InnerException != null)
                {
                    e = e.InnerException;
                    msg += e.Message;
                }
                Console.WriteLine(msg);
            }
        }
        static byte[] trimPadding(byte[] data, int len)
        {
            if (len > 0)
            {
                byte[] trimmed = new byte[data.Length - len];
                Array.Copy(data, 0, trimmed, 0, trimmed.Length);
                return trimmed;
            }
            else
                return data;
        }
        static byte[] prepareSwitch(mode currMode)
        {
            byte[] swdata = new byte[32];
            Array.Clear(swdata, 0, 32);
            switch (currMode)
            {
                case mode.enc:
                    swdata[0] = 0x01;
                    break;
                case mode.dec:
                    swdata[0] = 0x02;
                    break;
            }
            return swdata;
        }
    }
}
