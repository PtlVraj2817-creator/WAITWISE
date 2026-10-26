using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WaitWise.Models;

namespace WaitWise.Data
{
    public static class DbInitializer
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // 1. Ensure database is created and up to date
            await context.Database.MigrateAsync();

            // 2. Seed Roles
            string[] roles = { "Admin", "Staff" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // 3. Seed Admin User
            var adminEmail = "admin@waitwise.local";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "System Administrator",
                    EmailConfirmed = true,
                    Role = "Admin"
                };
                var result = await userManager.CreateAsync(adminUser, "Admin123!");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }

            // 4. Seed Staff User
            var staffEmail = "staff@waitwise.local";
            var staffUser = await userManager.FindByEmailAsync(staffEmail);
            if (staffUser == null)
            {
                staffUser = new ApplicationUser
                {
                    UserName = staffEmail,
                    Email = staffEmail,
                    FullName = "Sarah Jenkins",
                    EmailConfirmed = true,
                    Role = "Staff"
                };
                var result = await userManager.CreateAsync(staffUser, "Staff123!");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(staffUser, "Staff");
                }
            }

            // 5. Seed Departments
            if (!await context.Departments.AnyAsync())
            {
                var general = new Department
                {
                    Name = "General Consultation",
                    Description = "Primary healthcare checkups and general clinical triage",
                    CodePrefix = "A",
                    DefaultServiceTimeMinutes = 10,
                    IsActive = true
                };

                var billing = new Department
                {
                    Name = "Billing & Cashier",
                    Description = "Invoice payments, insurance verification, and receipts",
                    CodePrefix = "B",
                    DefaultServiceTimeMinutes = 5,
                    IsActive = true
                };

                var specialist = new Department
                {
                    Name = "Specialist Clinic",
                    Description = "Cardiology, Orthopedics, and specialist consultations",
                    CodePrefix = "C",
                    DefaultServiceTimeMinutes = 15,
                    IsActive = true
                };

                context.Departments.AddRange(general, billing, specialist);
                await context.SaveChangesAsync();

                // 6. Seed Counters
                var counters = new List<Counter>
                {
                    new Counter
                    {
                        Name = "Counter 1",
                        DepartmentId = general.Id,
                        AssignedStaffId = staffUser?.Id,
                        IsActive = true
                    },
                    new Counter
                    {
                        Name = "Counter 2",
                        DepartmentId = billing.Id,
                        IsActive = true
                    },
                    new Counter
                    {
                        Name = "Counter 3",
                        DepartmentId = specialist.Id,
                        IsActive = true
                    }
                };

                context.Counters.AddRange(counters);
                await context.SaveChangesAsync();

                // 7. Seed Initial Service Logs (Baseline historical data for the predictor)
                var random = new Random(42);
                var logs = new List<ServiceLog>();
                var now = DateTime.UtcNow;

                // Create baseline logs for General (approx 8-12 mins)
                for (int i = 0; i < 15; i++)
                {
                    var compTime = now.AddHours(-i);
                    logs.Add(new ServiceLog
                    {
                        DepartmentId = general.Id,
                        CounterId = counters[0].Id,
                        StaffUserId = staffUser?.Id,
                        DurationMinutes = Math.Round(8.0 + random.NextDouble() * 4.0, 1),
                        CompletedAt = compTime,
                        DayOfWeek = compTime.DayOfWeek,
                        HourOfDay = compTime.Hour
                    });
                }

                // Create baseline logs for Billing (approx 4-6 mins)
                for (int i = 0; i < 15; i++)
                {
                    var compTime = now.AddHours(-i);
                    logs.Add(new ServiceLog
                    {
                        DepartmentId = billing.Id,
                        CounterId = counters[1].Id,
                        DurationMinutes = Math.Round(3.5 + random.NextDouble() * 3.0, 1),
                        CompletedAt = compTime,
                        DayOfWeek = compTime.DayOfWeek,
                        HourOfDay = compTime.Hour
                    });
                }

                // Create baseline logs for Specialist (approx 14-18 mins)
                for (int i = 0; i < 15; i++)
                {
                    var compTime = now.AddHours(-i);
                    logs.Add(new ServiceLog
                    {
                        DepartmentId = specialist.Id,
                        CounterId = counters[2].Id,
                        DurationMinutes = Math.Round(13.0 + random.NextDouble() * 5.0, 1),
                        CompletedAt = compTime,
                        DayOfWeek = compTime.DayOfWeek,
                        HourOfDay = compTime.Hour
                    });
                }

                context.ServiceLogs.AddRange(logs);
                await context.SaveChangesAsync();
            }
        }
    }
}
