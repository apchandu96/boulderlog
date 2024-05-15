using boulderlog.Data;
using boulderlog.Data.Models;
using boulderlog.Domain;
using boulderlog.Domain.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Identity;
using System.IO.Compression;
using System.Threading.RateLimiting;

namespace boulderlog
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString), ServiceLifetime.Scoped, ServiceLifetime.Scoped);
            builder.Services.AddDatabaseDeveloperPageExceptionFilter();
            builder.Services.AddDefaultIdentity<AppUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Tokens.ProviderMap.Add("CustomEmailConfirmation", new TokenProviderDescriptor(typeof(AppEmailConfirmationTokenProvider<AppUser>)));
                options.Tokens.EmailConfirmationTokenProvider = "CustomEmailConfirmation";
            })
            .AddRoles<AppRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

            builder.Services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
                options.Providers.Add<BrotliCompressionProvider>();
                options.Providers.Add<GzipCompressionProvider>();
            });
            builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
            {
                options.Level = CompressionLevel.Fastest;
            });
            builder.Services.Configure<GzipCompressionProviderOptions>(options =>
            {
                options.Level = CompressionLevel.SmallestSize;
            });

            builder.Services.AddTransient<AppEmailConfirmationTokenProvider<AppUser>>();
            builder.Services.AddTransient<IEmailSender, EmailSender>();
            builder.Services.Configure<AppRateLimitOptions>(builder.Configuration.GetSection(AppRateLimitOptions.AppRateLimit));
            builder.Services.Configure<AppConfigOptions>(builder.Configuration.GetSection(AppConfigOptions.AppConfig));

            builder.Services.AddControllersWithViews();
            builder.Services.AddRazorPages();

            builder.Services.ConfigureApplicationCookie(o =>
            {
                o.ExpireTimeSpan = TimeSpan.FromDays(5);
                o.SlidingExpiration = true;
            });

            var rateLimitOptions = new AppRateLimitOptions();
            builder.Configuration.GetSection(AppRateLimitOptions.AppRateLimit).Bind(rateLimitOptions);
            if (rateLimitOptions != null)
            {
                builder.Services.AddRateLimiter(rateOptions =>
                {
                    rateOptions.AddSlidingWindowLimiter(policyName: rateLimitOptions.Policy, options =>
                    {
                        options.PermitLimit = rateLimitOptions.PermitLimit;
                        options.Window = TimeSpan.FromSeconds(rateLimitOptions.Window);
                        options.SegmentsPerWindow = rateLimitOptions.SegmentsPerWindow;
                        options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                        options.QueueLimit = rateLimitOptions.QueueLimit;
                        options.AutoReplenishment = rateLimitOptions.AutoReplenishment;
                    });
                    rateOptions.OnRejected = async (context, cancellationToken) =>
                    {
                        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                        {
                            context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString(System.Globalization.NumberFormatInfo.InvariantInfo);
                        }

                        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                        await context.HttpContext.Response.WriteAsync("Too many requests. Please try again later.", cancellationToken);
                    };
                    rateOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                });
            }

            var app = builder.Build();

            app.UseResponseCompression();
            app.UseRateLimiter();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseMigrationsEndPoint();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles(new StaticFileOptions(new Microsoft.AspNetCore.StaticFiles.Infrastructure.SharedOptions())
            {
                HttpsCompression = Microsoft.AspNetCore.Http.Features.HttpsCompressionMode.Compress,
                OnPrepareResponse = (fileContext) =>
                {
                    fileContext.Context.Response.GetTypedHeaders().CacheControl = new Microsoft.Net.Http.Headers.CacheControlHeaderValue()
                    {
                        Public = true,
                        MaxAge = TimeSpan.FromDays(1),
                        SharedMaxAge = TimeSpan.FromHours(1),
                    };
                },
            });

            app.UseRouting();

            app.UseCors();
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}").RequireRateLimiting(rateLimitOptions.Policy);
            app.MapRazorPages().RequireRateLimiting(rateLimitOptions.Policy);

            app.Run();
        }
    }
}