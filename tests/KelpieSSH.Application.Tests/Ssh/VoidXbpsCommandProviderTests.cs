using FluentAssertions;
using KelpieSSH.Application.Ssh;
namespace KelpieSSH.Application.Tests.Ssh;
public sealed class VoidXbpsCommandProviderTests
{
    private static SshConnectionProfile Profile(string os="void",string manager="xbps",KelpiePolicyMode mode=KelpiePolicyMode.ReadOnly)=>new(){Name="test",Host="example.invalid",UserName="deploy",PrivateKeyPath="id_ed25519",OsFamily=os,PackageManager=manager,Mode=mode};
    [Theory]
    [InlineData("void","xbps",true)]
    [InlineData("voidlinux","xbps",true)]
    [InlineData("VOIDLINUX","XBPS",true)]
    [InlineData("void","apt",false)]
    [InlineData("debian","xbps",false)]
    public void Supports_RequiresMatchingPlatform(string os,string manager,bool expected)=>new VoidXbpsCommandProvider().Supports(Profile(os,manager)).Should().Be(expected);
    [Theory]
    [InlineData("void")]
    [InlineData("voidlinux")]
    public void Resolve_UsesXbpsAndRegistersEightPackageOperations(string os)
    {
        PackageManagerResolver.Resolve(os,null).Should().Be("xbps");
        OsFamilyAliasResolver.Resolve(os).Should().Be("void");
        var commands=CommandProcessingProviderCatalog.CreateDefault().SelectMany(x=>x.GetCommands(Profile(os))).Where(x=>x.Name.StartsWith("pkg_",StringComparison.Ordinal)).ToArray();
        commands.Should().HaveCount(8);commands.Select(x=>x.Name).Should().OnlyHaveUniqueItems();
    }
    [Theory]
    [InlineData("pkg_check_updates","xbps-install -un",false)]
    [InlineData("pkg_info","xbps-query -R 'fixture'",false)]
    [InlineData("pkg_simulate_install","xbps-install -n 'fixture'",false)]
    [InlineData("pkg_simulate_remove","xbps-remove -n 'fixture'",false)]
    [InlineData("pkg_install","sudo -n xbps-install -y 'fixture'",true)]
    [InlineData("pkg_remove","sudo -n xbps-remove -y 'fixture'",true)]
    public void Render_PreservesReadOnlyAndConfirmationBoundary(string name,string expected,bool writes)
    {
        var c=new VoidXbpsCommandProvider().GetCommands(Profile()).Single(x=>x.Name==name);
        c.BuildCommandText(name=="pkg_check_updates"?null:new Dictionary<string,string>{{"package","fixture"}}).Should().Be(expected);
        c.RiskLevel.Should().Be(writes?SshCommandRiskLevel.ConfirmRequired:SshCommandRiskLevel.ReadOnly);
    }
    [Theory]
    [InlineData("-S")]
    [InlineData("a;id")]
    [InlineData("a\nwhoami")]
    [InlineData("$(id)")]
    [InlineData("a b")]
    [InlineData("a/b")]
    public void Render_RejectsShellOrOptionInjection(string package)
    {
        var c=new VoidXbpsCommandProvider().GetCommands(Profile()).Single(x=>x.Name=="pkg_install");
        var act=()=>c.BuildCommandText(new Dictionary<string,string>{{"package",package}});act.Should().Throw<InvalidOperationException>();
    }
    [Theory]
    [InlineData("pkg_search","query")]
    [InlineData("pkg_list_installed","filter")]
    public void Render_BoundsRowsWithoutRefreshingOrMaskingXbpsFailure(string name,string field)
    {
        var c=new VoidXbpsCommandProvider().GetCommands(Profile()).Single(x=>x.Name==name);
        var text=c.BuildCommandText(new Dictionary<string,string>{{field,"xbps"},{"limit","0"}});
        text.Should().Contain("code=$?").And.Contain("exit \"$code\"").And.Contain("head -n \"$2\"");
        text.Should().NotContain("-S").And.NotContain("-M");
        var act=()=>c.BuildCommandText(new Dictionary<string,string>{{field,"xbps"},{"limit","1000"}});act.Should().Throw<InvalidOperationException>();
    }
}
