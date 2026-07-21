using CpPrinting.Api.Data;
using CpPrinting.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CpPrinting.Api.Services
{
    public static class SuperAdminSeeder
    {
        private const string SuperAdminUsername = "superadmin";
        private const string InitialPassword = "s1948";

        public static async Task SeedAsync(
            IServiceProvider services)
        {
            using var scope = services.CreateScope();

            var context =
                scope.ServiceProvider
                    .GetRequiredService<AppDbContext>();

            var existingUser = await context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(user =>
                    user.Username == SuperAdminUsername
                );

            if (existingUser != null)
            {
                Console.WriteLine(
                    "[SuperAdminSeeder] SuperAdmin account already exists. " +
                    "No account changes were made."
                );

                return;
            }

            var superAdmin = new User
            {
                Id = Guid.NewGuid().ToString(),
                Username = SuperAdminUsername,
                PasswordHash =
                    BCrypt.Net.BCrypt.HashPassword(
                        InitialPassword
                    ),
                Name = "Super Administrator",
                Role = "SuperAdmin"
            };

            context.Users.Add(superAdmin);
            await context.SaveChangesAsync();

            Console.WriteLine(
                "[SuperAdminSeeder] SuperAdmin account created successfully."
            );
        }
    }
}