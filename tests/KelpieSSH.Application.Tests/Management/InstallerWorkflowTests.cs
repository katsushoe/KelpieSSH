using FluentAssertions;
using KelpieSSH.Application.Management;
namespace KelpieSSH.Application.Tests.Management;
public sealed class InstallerWorkflowTests
{
    [Theory] [InlineData(0)] [InlineData(3010)]
    public async Task Install_RunningServer_StopsInstallsAndRestartsInOrder(int code)
    {
        var calls=new List<string>();
        var result=await InstallerWorkflow.RunAsync(true,()=>{calls.Add("stop");return Task.CompletedTask;},()=>{calls.Add("install");return Task.FromResult(code);},()=>{calls.Add("start");return Task.CompletedTask;});
        result.Should().Be(code);calls.Should().Equal("stop","install","start");
    }
    [Fact] public async Task Install_StoppedServer_DoesNotChangeServerState()
    {
        var calls=new List<string>();await InstallerWorkflow.RunAsync(false,()=>{calls.Add("stop");return Task.CompletedTask;},()=>{calls.Add("install");return Task.FromResult(0);},()=>{calls.Add("start");return Task.CompletedTask;});calls.Should().Equal("install");
    }
    [Fact] public async Task Install_KnownFailure_RecoversWithoutRetry()
    {
        var calls=new List<string>();Func<Task> run=()=>InstallerWorkflow.RunAsync(true,()=>{calls.Add("stop");return Task.CompletedTask;},()=>{calls.Add("install");return Task.FromResult(1603);},()=>{calls.Add("start");return Task.CompletedTask;});
        await run.Should().ThrowAsync<InvalidOperationException>();calls.Should().Equal("stop","install","start");
    }
    [Fact] public async Task Install_UnknownOutcome_DoesNotRestartOrRepeat()
    {
        var calls=new List<string>();Func<Task> run=()=>InstallerWorkflow.RunAsync(true,()=>{calls.Add("stop");return Task.CompletedTask;},()=>{calls.Add("install");throw new InstallerOutcomeUnknownException("unknown",new TimeoutException());},()=>{calls.Add("start");return Task.CompletedTask;});
        await run.Should().ThrowAsync<InstallerOutcomeUnknownException>();calls.Should().Equal("stop","install");
    }
    [Fact] public async Task Install_StopDenied_DoesNotInstall()
    {
        var calls=new List<string>();Func<Task> run=()=>InstallerWorkflow.RunAsync(true,()=>{calls.Add("stop");throw new UnauthorizedAccessException();},()=>{calls.Add("install");return Task.FromResult(0);},()=>{calls.Add("start");return Task.CompletedTask;});
        await run.Should().ThrowAsync<UnauthorizedAccessException>();calls.Should().Equal("stop");
    }
    [Fact] public async Task Install_RestartFailure_IsNotReportedAsSuccess()
    {
        Func<Task> run=()=>InstallerWorkflow.RunAsync(true,()=>Task.CompletedTask,()=>Task.FromResult(0),()=>throw new IOException("restart failed"));
        await run.Should().ThrowAsync<IOException>();
    }
}
