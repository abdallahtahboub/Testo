using Autestiq.Contracts.Execution;

namespace Autestiq.Worker.Tests;

/// <summary>
/// What an exploration may do to an application beyond reading it. The rule that somebody else's
/// application is never pressed or typed into is the one most worth a test, so it is tested here
/// without a browser, one condition at a time.
/// </summary>
public class ExplorationPolicyTests
{
    private const string Ours = "http://127.0.0.1:5090";

    private static WorkerJob Job(
        string origin,
        IReadOnlyList<string> trusted,
        bool press = true,
        bool submit = true,
        WorkerTask task = WorkerTask.Explore) => new(
        RunId: Guid.CreateVersion7(),
        ScenarioId: "policy",
        TargetOrigin: origin,
        AllowedOrigins: [],
        TrustedLocalOrigins: trusted,
        Budget: RunBudget.Default,
        ArtifactDirectory: "unused",
        Task: task,
        ExploreStates: press,
        SubmitForms: submit);

    [Fact]
    public void AnApplicationWeRun_WhenTheJobAsks_MayBePressedAndTypedInto()
    {
        var job = Job(Ours, [Ours]);

        Assert.True(ExplorationPolicy.MayPressControls(job));
        Assert.True(ExplorationPolicy.MaySubmitForms(job));
    }

    [Fact]
    public void ACustomersApplication_IsRefused()
    {
        var job = Job("https://customer.example", []);

        Assert.False(ExplorationPolicy.MayPressControls(job));
        Assert.False(ExplorationPolicy.MaySubmitForms(job));
    }

    [Fact]
    public void ACustomersApplication_WhoseOwnerAllowsActing_MayBePressedAndTypedInto()
    {
        // ADR-0032: the dispatcher sets the flag only from the environment's stored permission.
        var job = Job("https://customer.example", []) with { CustomerAllowsActing = true };

        Assert.True(ExplorationPolicy.MayPressControls(job));
        Assert.True(ExplorationPolicy.MaySubmitForms(job));
    }

    [Fact]
    public void TheOwnersPermission_DoesNotReachAnythingButExploring_NorWhatTheJobDidNotAskFor()
    {
        var planned = Job("https://customer.example", [], task: WorkerTask.Planned) with { CustomerAllowsActing = true };
        Assert.False(ExplorationPolicy.MayPressControls(planned));

        var notAsked = Job("https://customer.example", [], press: false, submit: false) with { CustomerAllowsActing = true };
        Assert.False(ExplorationPolicy.MayPressControls(notAsked));
        Assert.False(ExplorationPolicy.MaySubmitForms(notAsked));

        var pressOnly = Job("https://customer.example", [], submit: false) with { CustomerAllowsActing = true };
        Assert.True(ExplorationPolicy.MayPressControls(pressOnly));
        Assert.False(ExplorationPolicy.MaySubmitForms(pressOnly));
    }

    [Fact]
    public void ACustomersApplication_IsRefused_EvenWhenTheCallerNamesItAsTrusted()
    {
        // The case that matters most. The run dispatcher names a run's own target as trusted-local
        // (R-11 in docs/security-register.md), so a caller's word is not enough: the address has to be
        // this machine's.
        var job = Job("https://customer.example", ["https://customer.example"]);

        Assert.False(ExplorationPolicy.MayPressControls(job));
        Assert.False(ExplorationPolicy.MaySubmitForms(job));
    }

    [Theory]
    [InlineData("http://localhost:5090")]
    [InlineData("http://shop.internal:5090")]
    [InlineData("http://192.168.1.20:5090")]
    [InlineData("http://10.0.0.5:5090")]
    public void AnAddressThatIsNotThisMachineByNumber_IsRefused(string origin)
    {
        // A name resolves to whatever its owner says, "localhost" included, and a private address is
        // somebody's machine, though not ours.
        var job = Job(origin, [origin]);

        Assert.False(ExplorationPolicy.MayPressControls(job));
    }

    [Fact]
    public void ThisMachine_WhenTheCallerDoesNotNameIt_IsRefused()
    {
        var job = Job(Ours, []);

        Assert.False(ExplorationPolicy.MayPressControls(job));
    }

    [Fact]
    public void ThisMachineByItsOtherNumber_IsRecognised()
    {
        var job = Job("http://[::1]:5090", ["http://[::1]:5090"]);

        Assert.True(ExplorationPolicy.MayPressControls(job));
    }

    [Fact]
    public void TheSameOriginWrittenWithATrailingSlash_IsRecognised()
    {
        var job = Job(Ours, [Ours + "/"]);

        Assert.True(ExplorationPolicy.MayPressControls(job));
    }

    [Fact]
    public void Typing_IsNotAllowedByPressingAlone()
    {
        var job = Job(Ours, [Ours], press: true, submit: false);

        Assert.True(ExplorationPolicy.MayPressControls(job));
        Assert.False(ExplorationPolicy.MaySubmitForms(job));
    }

    [Fact]
    public void Typing_WithoutPressing_IsNotAllowed()
    {
        var job = Job(Ours, [Ours], press: false, submit: true);

        Assert.False(ExplorationPolicy.MayPressControls(job));
        Assert.False(ExplorationPolicy.MaySubmitForms(job));
    }

    [Theory]
    [InlineData(WorkerTask.Scripted)]
    [InlineData(WorkerTask.Planned)]
    public void ARunThatIsNotAnExploration_IsRefused(WorkerTask task)
    {
        var job = Job(Ours, [Ours], task: task);

        Assert.False(ExplorationPolicy.MayPressControls(job));
        Assert.False(ExplorationPolicy.MaySubmitForms(job));
    }

    [Fact]
    public void AnOriginThatIsNotAnAddressAtAll_IsRefused()
    {
        var job = Job("not an address", ["not an address"]);

        Assert.False(ExplorationPolicy.MayPressControls(job));
    }
}
