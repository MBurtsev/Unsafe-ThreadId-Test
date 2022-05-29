// Maksim Burtsev https://github.com/MBurtsev
// Licensed under the MIT license.

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using DataflowBench.ConcurrentQueue;
using DataflowBench.MPOCnoOrder;
using DataflowChannel;
using System;

namespace DataflowBench
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // For debug
            //BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, new DebugInProcessConfig());

            // ChannelMPOCnoOrder
            //BenchmarkRunner.Run<MPOCnoOrderRead>();
            BenchmarkRunner.Run<MPOCnoOrderWrite>();
            //BenchmarkRunner.Run<MPOCnoOrderWriteWithReader>();

            Console.WriteLine("Press any key for exit");
            Console.ReadKey();
        }
    }
}
