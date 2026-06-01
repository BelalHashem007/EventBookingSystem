using Bogus;
using EventBookingSystem.Data;
using EventBookingSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace EventBookingSystem.Extensions
{
    public static class DatabaseExtensions
    {
        public static async Task ApplyMigrationAndSeedAsync(this IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

            try
            {
                logger.LogInformation("Checking for pending database migrations...");

                if ((await context.Database.GetPendingMigrationsAsync()).Any())
                {
                    await context.Database.MigrateAsync();
                    logger.LogInformation("Database migrations applied successfully.");
                }

                if (!await context.Events.AnyAsync())
                {
                    logger.LogInformation("Seeding database with initial development mock data...");

                    var eventFaker = new Faker<Event>()
                        .RuleFor(e => e.Name, f => f.Company.CatchPhrase())
                        .RuleFor(e => e.Description, f =>
                        {
                            var templates = new[]
                            {
                            $"Join us for an unforgettable night of {f.Music.Genre()} music featuring live performances, entertainment, and special guests.",

                            $"Experience an exciting {f.Commerce.Categories(1)[0]} event filled with networking opportunities, interactive sessions, and expert speakers.",

                            $"Get ready for an amazing evening at {f.Company.CompanyName()} where attendees will enjoy live activities, food, and entertainment.",

                            $"A premium event designed for fans of {f.Hacker.Noun()} culture with workshops, competitions, and exclusive experiences.",

                            $"Celebrate with hundreds of attendees in a high-energy atmosphere featuring performances, giveaways, and memorable moments.",

                            $"This event brings together enthusiasts and professionals for a full day of engaging talks, activities, and entertainment.",

                            $"Enjoy a unique experience with live shows, audience interaction, and opportunities to connect with others who share your interests."
                        };

                            return f.PickRandom(templates);
                        })
                        .RuleFor(e => e.Date, f => f.Date.Future(1))
                        .RuleFor(e => e.Venue, f => $"{f.Company.CompanyName()} Hall")
                        .RuleFor(e => e.ImageUrl, f => $"https://picsum.photos/seed/{f.Random.Guid()}/600/400")
                        .RuleFor(e => e.IsCancelled, f => false);

                    var ticketNames = new[]
                        {
                            "Standard",
                            "VIP",
                            "Premium",
                            "Backstage",
                            "Early Bird"
                        };

                    var seedEvents = new List<Event>();
                    for (var i=0; i<50; i++)
                    {
                        var fakeEvent = eventFaker.Generate();
                        int ticketCount = Random.Shared.Next(2, 5);

                        for(var j=0; j<ticketCount; j++)
                        {
                            fakeEvent.TicketTypes.Add(new TicketType
                            {
                                Name = ticketNames[j],
                                Price = Random.Shared.Next(300, 2000),
                                Quantity =  Random.Shared.Next(20, 200),
                            });
                        }
                        seedEvents.Add(fakeEvent);
                    }
                    await context.Events.AddRangeAsync(seedEvents);
                    await context.SaveChangesAsync();

                    logger.LogInformation("Database seeding completed. Mock rows successfully injected.");
                }
                else
                {
                    logger.LogInformation("Database already contains data. Skipping seeding phase.");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An unhandled error occurred during database migration or seeding processing.");
                throw; // Re-throw to intentionally halt application startup if the DB is broken
            }
        }
    }
}
