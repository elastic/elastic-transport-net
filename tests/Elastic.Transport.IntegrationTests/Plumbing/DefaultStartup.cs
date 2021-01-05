// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Elastic.Transport.IntegrationTests.Plumbing
{
	public class DefaultStartup
	{
		public DefaultStartup(IConfiguration configuration) => Configuration = configuration;

		public IConfiguration Configuration { get; }

		public void ConfigureServices(IServiceCollection services) => services.AddControllers();

		public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
		{
			if (env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
			}
			else
			{
				app.UseHsts();
			}

			app.UseHttpsRedirection();
			app.UseRouting();
			app.UseEndpoints(endpoints =>
			{
				MapEndpoints(endpoints);
				endpoints.MapControllerRoute("default", "{controller=Default}/{id?}");
			});
		}

		protected virtual void MapEndpoints(IEndpointRouteBuilder endpoints) { }
	}
}
