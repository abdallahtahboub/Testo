using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testo.Domain.Identity;
using Testo.Persistence.Identity;
using Testo.Persistence.Interceptors;

namespace Testo.Persistence;

/// <summary>Registers the database and the Identity pieces that read it.</summary>
public static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Adds the database context, the two save guards and the stores ASP.NET Core Identity uses.
    /// </summary>
    /// <param name="services">The container.</param>
    /// <param name="connectionString">Where PostgreSQL is.</param>
    public static IServiceCollection AddTestoPersistence(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<TenantGuardInterceptor>();
        services.AddScoped<StampInterceptor>();

        services.AddDbContext<TestoDbContext>((provider, options) =>
            options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(TestoDbContext).Assembly.FullName))
                .AddInterceptors(
                    provider.GetRequiredService<TenantGuardInterceptor>(),
                    provider.GetRequiredService<StampInterceptor>()));

        services.AddScoped<IUserStore<User>, UserStore>();

        services.AddIdentityCore<User>(options =>
            {
                // Length is the only requirement that makes passwords better. Composition rules
                // make them worse, and forced rotation makes them worse still
                // (docs/identity-and-access.md §1).
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;

                options.User.RequireUniqueEmail = true;

                // Slow an attacker down, then stop them — and always tell the person, with a way
                // back in (docs/identity-and-access.md §5).
                options.Lockout.MaxFailedAccessAttempts = 10;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

                options.SignIn.RequireConfirmedEmail = false;
            })
            // Only the authenticator provider, which checks a time-based one-time code. Password
            // reset and invitation links are rows of our own, so that each can be withdrawn, used
            // once, and shown in the audit log (see PasswordReset).
            .AddTokenProvider<AuthenticatorTokenProvider<User>>(TokenOptions.DefaultAuthenticatorProvider);

        return services;
    }
}
