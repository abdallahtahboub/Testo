using System.Net.Mail;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Autestiq.Application.Identity;
using Autestiq.Application.Tests.Support;
using Autestiq.Contracts.Identity;
using Autestiq.Persistence;

namespace Autestiq.Application.Tests.Identity;

/// <summary>
/// Email by SMTP and the first organization of a deployed instance (ADR-0040): the options refuse what cannot be sent
/// safely, the message carries the link in both languages and the token nowhere else, and an operator's organization
/// gets an owner who accepts from their mailbox like any member.
/// </summary>
public sealed class EmailAndBootstrapTests : IAsyncLifetime
{
    private const string Owner = "mara@contoso.example";

    private TestDatabase _database = null!;
    private SmtpSink _sink = null!;

    public async ValueTask InitializeAsync()
    {
        _database = await TestDatabase.CreateAsync();
        _sink = new SmtpSink();
    }

    public async ValueTask DisposeAsync()
    {
        await _sink.DisposeAsync();
        await _database.DisposeAsync();
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private SmtpOptions Local() => new()
    {
        Host = "127.0.0.1",
        Port = _sink.Port,
        StartTls = false,
        From = "no-reply@autestiq.example",
        PublicBaseUrl = "http://127.0.0.1:5080",
    };

    [Theory]
    [InlineData("", "no-reply@autestiq.example", "https://app.example", true, "Host")]
    [InlineData("smtp.example", "not an address", "https://app.example", true, "From")]
    [InlineData("smtp.example", "no-reply@autestiq.example", "/relative", true, "PublicBaseUrl")]
    [InlineData("smtp.example", "no-reply@autestiq.example", "http://app.example", true, "https")]
    [InlineData("smtp.example", "no-reply@autestiq.example", "https://app.example", false, "StartTls")]
    public void Options_ThatCannotSendSafely_SayWhy(string host, string from, string baseUrl, bool startTls, string named)
    {
        var options = new SmtpOptions { Host = host, From = from, PublicBaseUrl = baseUrl, StartTls = startTls };

        Assert.Contains(named, options.Problem(), StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddAutestiqSmtpEmail(options));
    }

    [Fact]
    public void Options_ForARelayOnThisMachine_AndAnHttpsSite_AreAccepted()
    {
        Assert.Null(Local().Problem());
        Assert.Null(new SmtpOptions { Host = "smtp.example", From = "a@b.example", PublicBaseUrl = "https://app.example" }.Problem());
    }

    [Fact]
    public void Link_IsAbsolute_WithTheTokenEscaped()
    {
        var link = new SmtpOptions { PublicBaseUrl = "https://app.example/" }.Link("/account/accept", "a+b/c=");

        Assert.Equal("https://app.example/account/accept?token=a%2Bb%2Fc%3D", link.AbsoluteUri);
    }

    [Fact]
    public void Paths_AreThePagesThatTakeTheToken()
    {
        Assert.Equal(Autestiq.Platform.Routes.AcceptInvitation, SmtpInvitationNotifier.AcceptPath);
        Assert.Equal(Autestiq.Platform.Routes.ResetPassword, SmtpInvitationNotifier.ResetPath);
    }

    [Fact]
    public void Compose_WritesEnglishThenGerman_WithTheLinkAndTheOrganization()
    {
        var (subject, body) = SmtpInvitationNotifier.Compose("Email_Invitation", new Uri("https://app.example/account/accept?token=t"), "Contoso");

        Assert.Equal("You are invited to Contoso / Sie sind zu Contoso eingeladen", subject);
        Assert.Contains("You have been invited to join Contoso.", body, StringComparison.Ordinal);
        Assert.Contains("Sie wurden eingeladen, Contoso beizutreten.", body, StringComparison.Ordinal);
        Assert.Equal(2, body.Split("https://app.example/account/accept?token=t").Length - 1);
    }

    [Fact]
    public async Task Send_DeliversToTheRelay_AndTheTokenIsInTheMessageButNotTheLog()
    {
        var log = new KeptLog();
        await using var services = new ServiceCollection()
            .AddLogging(builder => builder.AddProvider(log))
            .AddAutestiqSmtpEmail(Local())
            .BuildServiceProvider();

        await services.GetRequiredService<IInvitationNotifier>().SendPasswordResetAsync(Owner, "the-secret-token", Cancellation);

        var (from, to, data) = Assert.Single(_sink.Messages);
        Assert.Equal(("no-reply@autestiq.example", Owner), (from, Assert.Single(to)));
        Assert.Contains("token=the-secret-token", Decoded(data), StringComparison.Ordinal);
        Assert.DoesNotContain("the-secret-token", log.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateOrganization_InvitesItsOwnerByEmail_WhoAcceptsAndSignsInAsOwner()
    {
        await using var services = _database.NewServices(collection => collection.AddAutestiqSmtpEmail(Local()));

        var (organization, problem) = await CreateAsync(services, "contoso", "Contoso QA", Owner);

        Assert.Null(problem);
        var (_, to, data) = Assert.Single(_sink.Messages);
        Assert.Equal(Owner, Assert.Single(to));
        var token = TokenIn(Decoded(data));

        _database.Principal.OrganizationId = null;
        _database.Principal.UserId = null;
        using (var scope = services.CreateScope())
        {
            var accepted = await scope.ServiceProvider.GetRequiredService<IInvitationService>().AcceptAsync(token, "Mara Lindqvist", Seed.Password, Cancellation);
            Assert.True(accepted.Succeeded, string.Join(", ", accepted.Errors));
        }

        using (var scope = services.CreateScope())
        {
            var (result, ticket) = await scope.ServiceProvider.GetRequiredService<SignInWorkflow>().AuthenticateAsync(Owner, Seed.Password, "127.0.0.1", Cancellation);
            Assert.Equal(SignInOutcome.Succeeded, result.Outcome);
            Assert.Equal((organization, Role.Owner), (ticket!.OrganizationId, ticket.Role));
        }

        await using var database = _database.NewContext();
        var actions = await database.AuditEntries.IgnoreQueryFilters().Where(entry => entry.OrganizationId == organization).Select(entry => entry.Action).ToListAsync(Cancellation);
        Assert.Contains(OrganizationBootstrap.Created, actions);
        Assert.Contains("member.invited", actions);
    }

    [Theory]
    [InlineData("Contoso", "Contoso QA", Owner, "slug")]
    [InlineData("contoso", "  ", Owner, "name")]
    [InlineData("contoso", "Contoso QA", "not-an-address", "address")]
    public async Task CreateOrganization_WithBadInput_IsRefused_AndSendsNothing(string slug, string name, string owner, string named)
    {
        await using var services = _database.NewServices(collection => collection.AddAutestiqSmtpEmail(Local()));

        var (organization, problem) = await CreateAsync(services, slug, name, owner);

        Assert.Null(organization);
        Assert.Contains(named, problem, StringComparison.Ordinal);
        Assert.Empty(_sink.Messages);
    }

    [Fact]
    public async Task CreateOrganization_WithASlugInUse_IsRefused()
    {
        await using var services = _database.NewServices(collection => collection.AddAutestiqSmtpEmail(Local()));
        Assert.Null((await CreateAsync(services, "contoso", "Contoso QA", Owner)).Problem);

        var (organization, problem) = await CreateAsync(services, "contoso", "Another", "other@contoso.example");

        Assert.Null(organization);
        Assert.Contains("exists", problem, StringComparison.Ordinal);
        Assert.Single(_sink.Messages);
    }

    private Task<(Guid? OrganizationId, string? Problem)> CreateAsync(ServiceProvider services, string slug, string name, string owner)
    {
        _database.Principal.OrganizationId = null;
        var scope = services.CreateScope();
        return OrganizationBootstrap.CreateAsync(
            scope.ServiceProvider.GetRequiredService<AutestiqDbContext>(),
            scope.ServiceProvider.GetRequiredService<TenantWriteScope>(),
            scope.ServiceProvider.GetRequiredService<IInvitationNotifier>(),
            TimeProvider.System,
            slug,
            name,
            owner,
            "the rehearsal",
            Cancellation);
    }

    /// <summary>The message as a person reads it: quoted-printable or base64 parts undone.</summary>
    private static string Decoded(string data)
    {
        var bodyStart = data.IndexOf("\n\n", StringComparison.Ordinal);
        var headers = data[..bodyStart];
        var body = data[(bodyStart + 2)..];

        if (headers.Contains("base64", StringComparison.OrdinalIgnoreCase))
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(body.Replace("\n", "", StringComparison.Ordinal)));
        }

        // Quoted-printable: soft line breaks, then =XX escapes.
        var joined = body.Replace("=\n", "", StringComparison.Ordinal);
        var bytes = new List<byte>();

        for (var index = 0; index < joined.Length; index++)
        {
            if (joined[index] == '=' && index + 2 < joined.Length && Uri.IsHexDigit(joined[index + 1]) && Uri.IsHexDigit(joined[index + 2]))
            {
                bytes.Add(Convert.ToByte(joined.Substring(index + 1, 2), 16));
                index += 2;
            }
            else
            {
                bytes.AddRange(Encoding.UTF8.GetBytes(joined[index].ToString()));
            }
        }

        return Encoding.UTF8.GetString([.. bytes]);
    }

    private static string TokenIn(string text)
    {
        var start = text.IndexOf("token=", StringComparison.Ordinal) + "token=".Length;
        var end = text.IndexOfAny([' ', '\n', '\r'], start);
        return Uri.UnescapeDataString(text[start..(end < 0 ? text.Length : end)]);
    }

    /// <summary>Everything logged, as one text.</summary>
    private sealed class KeptLog : ILoggerProvider
    {
        private readonly StringBuilder _text = new();

        public string Text => _text.ToString();

        public ILogger CreateLogger(string categoryName) => new Logger(_text);

        public void Dispose()
        {
        }

        private sealed class Logger(StringBuilder text) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                text.Append(formatter(state, exception)).Append('\n');
        }
    }
}
