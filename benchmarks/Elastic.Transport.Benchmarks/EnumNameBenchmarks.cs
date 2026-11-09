// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Runtime.Serialization;
using BenchmarkDotNet.Attributes;
using Elastic.Transport.Extensions;

namespace Elastic.Transport.Benchmarks;

public enum Refresh
{
	[EnumMember(Value = "true")] True,
	[EnumMember(Value = "wait_for")] WaitFor,
	NoAttribute,
}

/// <summary>Turning an enum query-string value into its wire name, which happens for every such value of every request.</summary>
public class EnumNameBenchmarks
{
	private readonly Enum _withAttribute = Refresh.WaitFor;
	private readonly Enum _withoutAttribute = Refresh.NoAttribute;

	[Benchmark(Baseline = true)]
	public string WithEnumMember() => _withAttribute.GetEnumName();

	[Benchmark]
	public string WithoutEnumMember() => _withoutAttribute.GetEnumName();
}
