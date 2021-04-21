﻿// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace Elastic.Transport.Benchmarks
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			var config = ManualConfig
				.Create(DefaultConfig.Instance)
				.AddDiagnoser(MemoryDiagnoser.Default)
				.WithSummaryStyle(new SummaryStyle(null, false, SizeUnit.B, null));

			BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
		}
	}
}
