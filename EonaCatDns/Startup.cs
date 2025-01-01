using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WebMarkupMin.AspNetCore6;

namespace EonaCat.Dns;

public class Startup
{
    private readonly IConfiguration _configuration;

    public Startup(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        // Configure limited memory caching
        services.AddDistributedMemoryCache(options =>
        {
            options.SizeLimit = 512 * 1024 * 1024;
        });

        services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromMinutes(20);
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
        });

        services.AddMvc(options => options.EnableEndpointRouting = false);

        // Configure WebMarkupMin
        services.AddWebMarkupMin(
                options =>
                {
                    options.AllowMinificationInDevelopmentEnvironment = true;
                    options.AllowCompressionInDevelopmentEnvironment = true;
                })
            .AddHtmlMinification(
                options =>
                {
                    options.MinificationSettings.RemoveRedundantAttributes = true;
                    options.MinificationSettings.RemoveHttpProtocolFromAttributes = true;
                    options.MinificationSettings.RemoveHttpsProtocolFromAttributes = true;
                })
            .AddHttpCompression();

        // Optimize response compression
        services.AddResponseCompression(options =>
        {
            options.Providers.Add<GzipCompressionProvider>();
            options.Providers.Add<BrotliCompressionProvider>();
            options.EnableForHttps = true;

            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
                new[] { "text/javascript", "application/json" }
            );
        });

        // Configure logging
        services.AddLogging(config =>
        {
            config.ClearProviders();
            config.AddDebug();
            config.AddEventSourceLogger();

            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == Environments.Development)
            {
                config.AddConsole();
            }
        });

        // Set threading optimizations
        ThreadPool.SetMinThreads(4, 4); // Minimum threads
        ThreadPool.SetMaxThreads(50, 50); // Cap threads to 50 to limit concurrency
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/Index/Error");
            app.UseHsts();
        }

        app.UseStaticFiles();

        app.UseResponseCompression();

        app.UseSession();

        app.UseWebMarkupMin();

        app.UseMvc(routes =>
        {
            routes.MapRoute(
                "EonaCatDns",
                "{controller=Index}/{action=Index}/{id?}");
        });

        app.SetBaseUrl();

        MonitorMemoryUsage();
    }

    private static void MonitorMemoryUsage()
    {
        _ = Task.Run(async () =>
        {
            while (true)
            {
                // Check current memory usage
                var memoryUsed = GC.GetTotalMemory(false);
                if (memoryUsed > 1.8 * 1024 * 1024 * 1024)
                {
                    Console.WriteLine($"Warning: High memory usage detected: {memoryUsed / (1024 * 1024)} MB.");
                    GC.Collect();
                }
                await Task.Delay(TimeSpan.FromSeconds(10));
            }
        });
    }
}
