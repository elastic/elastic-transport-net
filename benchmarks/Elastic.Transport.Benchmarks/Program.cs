using System;
using BenchmarkDotNet.Running;

namespace Elastic.Transport.Benchmarks
{
	internal class Program
	{
		private static void Main(string[] args) => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
	}
}
