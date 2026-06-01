using EventBookingSystem.Configuration;
using EventBookingSystem.Data;
using EventBookingSystem.DomainEvents.Dispatcher;
using EventBookingSystem.DomainEvents.Handlers;
using EventBookingSystem.DomainEvents.Events;
using EventBookingSystem.Exceptions;
using EventBookingSystem.Models;
using EventBookingSystem.Repositories;
using EventBookingSystem.Repositories.Interfaces;
using EventBookingSystem.Services;
using EventBookingSystem.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;
using EventBookingSystem.BackgroundJobs;
using EventBookingSystem.Hubs;
using EventBookingSystem.Extensions;

namespace EventBookingSystem
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var egpCulture = new CultureInfo("en-EG");
            egpCulture.NumberFormat.CurrencyPositivePattern = 2;
            CultureInfo.DefaultThreadCurrentCulture = egpCulture;
            CultureInfo.DefaultThreadCurrentUICulture = egpCulture;

            builder.Services.Configure<RequestLocalizationOptions>(options =>
            {
                options.DefaultRequestCulture = new RequestCulture(egpCulture);
                options.SupportedCultures = [egpCulture];
                options.SupportedUICultures = [egpCulture];
            });
            builder.Services.Configure<MailSettings>(builder.Configuration.GetSection("MailSettings"));
            builder.Services.Configure<ApplicationSettings>(builder.Configuration.GetSection("ApplicationSettings"));

            // Add services to the container.
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<IEventService, EventService>();
            builder.Services.AddScoped<IDashboardService, DashboardService>();
            builder.Services.AddScoped<IBookingService, BookingService>();
            builder.Services.AddScoped<IUserService, UserService>();
            builder.Services.AddScoped<IPaymentService, PaymentService>();
            builder.Services.AddScoped<IEmailService, EmailSerivce>();
            builder.Services.AddScoped<IWebhookService, WebhookService>();
            builder.Services.AddScoped<INotificationService, NotificationService>();
            builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
            builder.Services.AddScoped(typeof(IBaseRepository<>), typeof(BaseRepository<>));
            builder.Services.AddScoped<IBookingRepository, BookingRepository>();
            builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
            builder.Services.AddScoped<IdentitySeeder>();
            builder.Services.AddControllersWithViews();
            builder.Services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
            });
            builder.Services.AddIdentity<ApplicationUser, IdentityRole<int>>(options =>
            {
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireDigit = false;
            })
            .AddEntityFrameworkStores<AppDbContext>();

            builder.Services.ConfigureApplicationCookie(options =>
            {
                options.Events.OnRedirectToLogin = context =>
                {
                    var returnUrl = string.Concat(context.Request.PathBase, context.Request.Path, context.Request.QueryString);
                    var encodedReturnUrl = Uri.EscapeDataString(returnUrl);

                    context.Response.Redirect($"/?authModal=login&returnUrl={encodedReturnUrl}");
                    return Task.CompletedTask;
                };
            });

            //signalr 
            builder.Services.AddSignalR();

            //hosted services
            builder.Services.AddHostedService<BookingExpiryHostedService>();
            builder.Services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
            builder.Services.AddHostedService<DomainEventHostedService>();

            //domain event
            builder.Services.AddScoped<IEventHandler<BookingCreatedEvent>, BookingCreatedEmailHandler>();
            builder.Services.AddScoped<IEventHandler<BookingConfirmedEvent>, BookingConfirmedEmailHandler>();
            builder.Services.AddScoped<IEventHandler<BookingCreatedEvent>, BookingCreatedNotificationHandler>();
            builder.Services.AddScoped<IEventHandler<BookingConfirmedEvent>, BookingConfirmationNotificationHandler>();
            builder.Services.AddScoped<IEventDispatcher, EventDispatcher>();


            var app = builder.Build();

            //apply migration and seed events table
            await app.Services.ApplyMigrationAndSeedAsync();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseRequestLocalization(app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value);
            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapStaticAssets();
            app.MapHub<NotificationHub>("/notificationHub");
            app.MapControllerRoute(
                name: "areas",
                pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}")
                .WithStaticAssets();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}")
                .WithStaticAssets();

            using (var scope = app.Services.CreateScope())
            {
                try
                {
                    var identitySeeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
                    await identitySeeder.SeedAdminAccountAsync();
                }
                catch (AdminSeedException ex)
                {
                    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                    logger.LogCritical(ex, "Identity Seeding failed. The app will continue, but admin features may be unavailable.");
                }
                catch (Exception ex)
                {
                    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ex, "An unexpected error occurred during startup.");
                }
            }

            app.Run();
        }
    }
}
