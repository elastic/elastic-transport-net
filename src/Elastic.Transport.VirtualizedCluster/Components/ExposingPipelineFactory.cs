/*
 * Licensed to Elasticsearch B.V. under one or more contributor
 * license agreements. See the NOTICE file distributed with
 * this work for additional information regarding copyright
 * ownership. Elasticsearch B.V. licenses this file to you under
 * the Apache License, Version 2.0 (the "License"); you may
 * not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *    http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied.  See the License for the
 * specific language governing permissions and limitations
 * under the License.
 */

namespace Elastic.Transport.VirtualizedCluster.Components
{
	/// <summary>
	/// An implementation that exposes all the components so that <see cref="VirtualCluster"/> can reference them directly.
	/// </summary>
	public class ExposingPipelineFactory<TConfiguration> : IRequestPipelineFactory<TConfiguration> where TConfiguration : class, ITransportConfiguration
	{
		public ExposingPipelineFactory(TConfiguration connectionSettings, IDateTimeProvider dateTimeProvider)
		{
			DateTimeProvider = dateTimeProvider;
			MemoryStreamFactory = TransportConfiguration.DefaultMemoryStreamFactory;

			Settings = connectionSettings;
			Pipeline = Create(Settings, DateTimeProvider, MemoryStreamFactory, new RequestParameters());
			Transport = new Transport<TConfiguration>(Settings, this, DateTimeProvider, MemoryStreamFactory);
		}

		// ReSharper disable once MemberCanBePrivate.Global
		public IRequestPipeline Pipeline { get; }

		private IDateTimeProvider DateTimeProvider { get; }
		private IMemoryStreamFactory MemoryStreamFactory { get; }
		private TConfiguration Settings { get; }
		public ITransport<ITransportConfiguration> Transport { get; }


		public IRequestPipeline Create(TConfiguration configurationValues, IDateTimeProvider dateTimeProvider,
			IMemoryStreamFactory memoryStreamFactory, IRequestParameters requestParameters
		) =>
			new RequestPipeline<TConfiguration>(Settings, DateTimeProvider, MemoryStreamFactory, requestParameters ?? new RequestParameters());
	}
}
