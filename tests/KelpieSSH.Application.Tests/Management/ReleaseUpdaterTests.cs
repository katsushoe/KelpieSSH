using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using KelpieSSH.Application.Management;
namespace KelpieSSH.Application.Tests.Management;

public sealed class ReleaseUpdaterTests
{
    private static readonly byte[] Content = Encoding.UTF8.GetBytes("synthetic MSI fixture");
    private static string Hash => Convert.ToHexString(SHA256.HashData(Content)).ToLowerInvariant();
    private static JsonObject Metadata() => JsonNode.Parse(JsonSerializer.Serialize(new {
        draft=false, prerelease=false, tag_name="v0.3.16.0", assets=new[] {new {
            name="KelpieSSH-0.3.16.0-x64.msi", digest="sha256:"+Hash, size=Content.Length,
            browser_download_url="https://github.com/example/project/releases/download/v0.3.16.0/KelpieSSH-0.3.16.0-x64.msi"
        }} }))!.AsObject();
    private static MsiRelease? Select(JsonObject value) => ReleaseUpdater.Select(Encoding.UTF8.GetBytes(value.ToJsonString()),new Version(0,3,15,0));
    [Fact] public void Select_NewStableMsi_RequiresExactNameHashAndSize()
    {
        var r=Select(Metadata()); r.Should().NotBeNull(); r!.Version.Should().Be(new Version(0,3,16,0));
        r.Sha256.Should().Be(Hash); r.Size.Should().Be(Content.Length);
    }
    [Theory] [InlineData("draft")] [InlineData("prerelease")]
    public void Select_NonStable_ReturnsNoUpdate(string flag) { var m=Metadata();m[flag]=true;Select(m).Should().BeNull(); }
    [Theory] [InlineData("v0.3.15.0")] [InlineData("v0.3.14.0")]
    public void Select_NotNewer_ReturnsNoUpdate(string version) {var m=Metadata();m["tag_name"]=version;Select(m).Should().BeNull();}
    [Theory] [InlineData("0.3.16")] [InlineData("v0.3.16.0-alpha")] [InlineData("vv0.3.16.0")]
    public void Select_InvalidVersion_Rejects(string version) {var m=Metadata();m["tag_name"]=version;Action call=()=>Select(m);call.Should().Throw<InvalidDataException>();}
    [Theory] [InlineData("name","unrelated.msi")] [InlineData("digest","")] [InlineData("digest","sha256:xyz")]
    [InlineData("browser_download_url","http://example.invalid/update.msi")]
    [InlineData("browser_download_url","https://name:secret@example.invalid/update.msi")]
    public void Select_InvalidAsset_Rejects(string key,string value) {var m=Metadata();m["assets"]![0]![key]=value;Action call=()=>Select(m);call.Should().Throw<InvalidDataException>();}
    [Theory] [InlineData(0)] [InlineData(-1)] [InlineData(536870913)]
    public void Select_InvalidSize_Rejects(long size) {var m=Metadata();m["assets"]![0]!["size"]=size;Action call=()=>Select(m);call.Should().Throw<InvalidDataException>();}
    [Fact] public void Select_DuplicateAsset_Rejects() {var m=Metadata();m["assets"]!.AsArray().Add(m["assets"]![0]!.DeepClone());Action call=()=>Select(m);call.Should().Throw<InvalidDataException>();}
    [Fact] public void Select_MissingDigest_Rejects() {var m=Metadata();m["assets"]![0]!.AsObject().Remove("digest");Action call=()=>Select(m);call.Should().Throw<InvalidDataException>();}
    [Fact] public void Select_OversizeMetadata_Rejects() {Action call=()=>ReleaseUpdater.Select(new byte[1048577],new Version(0,0,0,0));call.Should().Throw<InvalidDataException>();}
    [Fact] public async Task Check_NotFound_ReturnsNoUpdate()
    {
        using var client=new HttpClient(new Stub(_=>new HttpResponseMessage(HttpStatusCode.NotFound)));
        (await new ReleaseUpdater(client).CheckAsync("https://api.github.com/repos/example/project/releases/latest",new Version(0,3,15,0),default)).Should().BeNull();
    }
    [Fact] public async Task Check_OutsideRepository_Rejects()
    {
        var m=Metadata();m["assets"]![0]!["browser_download_url"]="https://example.invalid/other.msi";
        using var client=new HttpClient(new Stub(_=>new(HttpStatusCode.OK){Content=new StringContent(m.ToJsonString())}));
        Func<Task> call=()=>new ReleaseUpdater(client).CheckAsync("https://api.github.com/repos/example/project/releases/latest",new Version(0,3,15,0),default);
        await call.Should().ThrowAsync<InvalidDataException>();
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Download_GoodOrWrongDigest_VerifiesBeforeCompletion(bool corrupt)
    {
        var folder=Path.Combine(Path.GetTempPath(),"kelpie-update-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        var path=Path.Combine(folder,"update.msi");
        try {
            using var client=new HttpClient(new Stub(_=>new(HttpStatusCode.OK){Content=new ByteArrayContent(Content)}));
            var r=Select(Metadata())!;if(corrupt)r=r with{Sha256=new string('0',64)};
            Func<Task> call=()=>new ReleaseUpdater(client).DownloadAsync(r,path,default);
            if(corrupt){await call.Should().ThrowAsync<InvalidDataException>();File.Exists(path).Should().BeFalse();}
            else{await call();(await File.ReadAllBytesAsync(path)).Should().Equal(Content);}
        } finally {Directory.Delete(folder,true);}
    }
    [Fact] public async Task Download_ExistingFile_DoesNotDeleteIt()
    {
        var path=Path.GetTempFileName();await File.WriteAllTextAsync(path,"existing");
        try{using var client=new HttpClient(new Stub(_=>new(HttpStatusCode.OK){Content=new ByteArrayContent(Content)}));
            Func<Task> call=()=>new ReleaseUpdater(client).DownloadAsync(Select(Metadata())!,path,default);
            await call.Should().ThrowAsync<IOException>();(await File.ReadAllTextAsync(path)).Should().Be("existing");
        }finally{File.Delete(path);}
    }
    [Fact] public async Task Download_SizeOverflow_DeletesPartialFile()
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N")+".msi");
        using var client=new HttpClient(new Stub(_=>new(HttpStatusCode.OK){Content=new ByteArrayContent(Content)}));
        Func<Task> call=()=>new ReleaseUpdater(client).DownloadAsync(Select(Metadata())! with{Size=1},path,default);
        await call.Should().ThrowAsync<InvalidDataException>();File.Exists(path).Should().BeFalse();
    }
    private sealed class Stub(Func<HttpRequestMessage,HttpResponseMessage> response):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {ct.ThrowIfCancellationRequested();var r=response(request);r.RequestMessage=request;return Task.FromResult(r);}
    }
}

public sealed class TrayPreferencesTests
{
    [Fact] public void Save_RoundTrip_PreservesPreferencesAndNoTemporaryFiles()
    {
        var dir=Path.Combine(Path.GetTempPath(),"kelpie-settings-"+Guid.NewGuid().ToString("N"));var path=Path.Combine(dir,"tray.json");
        try {var value=new TrayPreferences{AutoStartServer=false,StartAtLogon=true};value.Save(path);TrayPreferences.Load(path).Should().Be(value);Directory.GetFiles(dir).Should().HaveCount(1);}
        finally {if(Directory.Exists(dir))Directory.Delete(dir,true);}
    }
    [Theory] [InlineData("http://example.invalid/latest")] [InlineData("https://user:password@example.invalid/latest")]
    public void Save_InvalidSource_PreservesExistingSettings(string source)
    {
        var dir=Path.Combine(Path.GetTempPath(),"kelpie-settings-"+Guid.NewGuid().ToString("N"));var path=Path.Combine(dir,"tray.json");
        try{new TrayPreferences().Save(path);var before=File.ReadAllText(path);Action call=()=>new TrayPreferences{ReleaseApiUrl=source}.Save(path);call.Should().Throw<ArgumentException>();File.ReadAllText(path).Should().Be(before);}
        finally{Directory.Delete(dir,true);}
    }
    [Fact] public void Paths_DifferentInstallations_AreIsolated()
    {
        var root=Path.GetTempPath();ManagementPaths.UserDirectory(root,Path.Combine(root,"one")).Should().NotBe(ManagementPaths.UserDirectory(root,Path.Combine(root,"two")));
    }
    [Fact] public void Paths_InstallerAndLauncherSpellings_NormalizeToSameRoot()
    {
        var plain=Path.Combine(Path.GetTempPath(),"kelpie-install");
        var installerForm=plain+Path.DirectorySeparatorChar+".";
        var trailing=plain+Path.DirectorySeparatorChar;
        ManagementPaths.NormalizeInstallRoot(installerForm).Should().Be(plain);
        ManagementPaths.NormalizeInstallRoot(trailing).Should().Be(plain);
        ManagementPaths.Identity(installerForm).Should().Be(ManagementPaths.Identity(plain));
    }
}
