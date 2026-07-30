using FluentAssertions;
using KelpieMCPServer;
using KelpieSSH.Application.Ssh;

namespace KelpieSSH.Application.Tests.Ssh;

public sealed class WebBulkTransferStoreTests
{
    [Fact]
    public void AddFile_WhenRemotePathIsDuplicate_ShouldRejectSecondFile()
    {
        var store = new WebBulkTransferStore();
        var transfer = store.Create("example", "primary");
        var first = CreateItem("a.txt", "/index.html");
        var second = CreateItem("b.txt", "/index.html");

        store.AddFile(transfer.Id, first);
        var action = () => store.AddFile(transfer.Id, second);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*already registered*");
    }

    [Fact]
    public void SetState_WhenExpectedStateDoesNotMatch_ShouldRejectTransition()
    {
        var store = new WebBulkTransferStore();
        var transfer = store.Create("example", "primary");

        var action = () => store.SetState(
            transfer.Id,
            WebBulkTransferState.Applied,
            WebBulkTransferState.Committed);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*must be Applied*");
    }

    [Fact]
    public void Cancel_WhenTransferIsApplied_ShouldRetainTransfer()
    {
        var store = new WebBulkTransferStore();
        var transfer = store.Create("example", "primary");
        store.SetState(transfer.Id, WebBulkTransferState.Draft, WebBulkTransferState.Applied);

        var action = () => store.Cancel(transfer.Id);

        action.Should().Throw<InvalidOperationException>();
        store.Get(transfer.Id).State.Should().Be(WebBulkTransferState.Applied);
    }

    [Fact]
    public async Task Execute_WhenLocalFileWasReplaced_ShouldRejectBeforeSshTransfer()
    {
        var path = Path.Combine(Path.GetTempPath(), "kelpie-bulk-source-" + Guid.NewGuid().ToString("N"));
        await File.WriteAllTextAsync(path, "first");
        try
        {
            var store = new WebBulkTransferStore();
            var transfer = store.Create("example", "primary");
            transfer = await KelpieTools.AddWebBulkTransferFileAsync(store, transfer.Id, path, "/index.html");
            var manifest = new string('b', 64);
            transfer = store.SetState(transfer.Id, WebBulkTransferState.Draft, WebBulkTransferState.Validated, manifest);
            await File.WriteAllTextAsync(path, "other");

            var action = async () => await KelpieTools.ExecuteWebBulkTransferAsync(
                null!,
                null!,
                null!,
                store,
                transfer.Id,
                $"web_bulk_transfer_execute:{transfer.Id}:{manifest}");

            await action.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*changed after registration*");
            store.Get(transfer.Id).State.Should().Be(WebBulkTransferState.Validated);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static WebBulkTransferItem CreateItem(string localPath, string remotePath)
    {
        return new WebBulkTransferItem(
            localPath,
            remotePath,
            1,
            new string('a', 64),
            "text/plain",
            null,
            null);
    }
}
