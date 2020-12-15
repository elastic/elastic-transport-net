// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

 using System;
 using System.Threading;

namespace Elastic.Transport.VirtualizedCluster.Rules
{
	public interface IRule
	{
		/// <summary>The value or exception to return after the call succeeds</summary>
		RuleOption<Exception, int> AfterSucceeds { get; set; }

		/// <summary>This rule is constrain on the node with this port number</summary>
		int? OnPort { get; set; }

		/// <summary> Either a hard exception or soft HTTP error code</summary>
		RuleOption<Exception, int> Return { get; set; }

		/// <summary>Set an explicit return content type for the API call</summary>
		string ReturnContentType { get; set; }

		/// <summary>Explicitly set the bytes returned by the API call, optional.</summary>
		byte[] ReturnResponse { get; set; }

		/// <summary>Whether this rule describes an API call succeeding or not</summary>
		bool Succeeds { get; set; }

		/// <summary>Simulate a long running call</summary>
		TimeSpan? Takes { get; set; }

		/// <summary> The number of times this rule stays valid after being called</summary>
		RuleOption<TimesHelper.AllTimes, int> Times { get; set; }

		/// <summary> The amount of times this rule has been executed</summary>
		int Executed { get; }

		/// <summary> Mark a rule as executed </summary>
		void RecordExecuted();
	}

	public abstract class RuleBase<TRule> : IRule
		where TRule : RuleBase<TRule>, IRule
	{
		private int _executed;
		RuleOption<Exception, int> IRule.AfterSucceeds { get; set; }
		int? IRule.OnPort { get; set; }
		RuleOption<Exception, int> IRule.Return { get; set; }
		string IRule.ReturnContentType { get; set; }
		byte[] IRule.ReturnResponse { get; set; }
		private IRule Self => this;
		bool IRule.Succeeds { get; set; }
		TimeSpan? IRule.Takes { get; set; }
		RuleOption<TimesHelper.AllTimes, int> IRule.Times { get; set; }

		int IRule.Executed => _executed;

		void IRule.RecordExecuted() => Interlocked.Increment(ref _executed);

		public TRule OnPort(int port)
		{
			Self.OnPort = port;
			return (TRule)this;
		}

		public TRule Takes(TimeSpan span)
		{
			Self.Takes = span;
			return (TRule)this;
		}

		public TRule ReturnResponse<T>(T response)
			where T : class
		{
			byte[] r;
			using (var ms = TransportConfiguration.DefaultMemoryStreamFactory.Create())
			{
				LowLevelRequestResponseSerializer.Instance.Serialize(response, ms);
				r = ms.ToArray();
			}
			Self.ReturnResponse = r;
			Self.ReturnContentType = RequestData.MimeType;
			return (TRule)this;
		}

		public TRule ReturnByteResponse(byte[] response, string responseContentType = RequestData.MimeType)
		{
			Self.ReturnResponse = response;
			Self.ReturnContentType = responseContentType;
			return (TRule)this;
		}
	}
}
