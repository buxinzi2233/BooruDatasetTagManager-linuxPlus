using Bdtm.Core;
using Xunit;

namespace Bdtm.Core.Tests;

public class CharacterTagFileTransactionTests : IDisposable
{
    private readonly string _tempRoot;

    public CharacterTagFileTransactionTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "bdtm-char-txn-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, recursive: true);
        }
        catch
        {
            // best-effort
        }
    }

    [Fact]
    public async Task CommitAsync_WritesNewContentAndCleansTxnDir()
    {
        string target = Path.Combine(_tempRoot, "a.txt");
        await File.WriteAllTextAsync(target, "old");

        await CharacterTagFileTransaction.CommitAsync(
            _tempRoot,
            new[] { new CharacterTagFileChange(target, "new tags here") });

        Assert.Equal("new tags here", await File.ReadAllTextAsync(target));
        Assert.Empty(Directory.GetDirectories(_tempRoot, CharacterTagFileTransaction.DirectoryPrefix + "*"));
    }

    [Fact]
    public async Task CommitAsync_CreatesMissingFile()
    {
        string target = Path.Combine(_tempRoot, "subdir", "new.txt");

        await CharacterTagFileTransaction.CommitAsync(
            _tempRoot,
            new[] { new CharacterTagFileChange(target, "created content") });

        Assert.True(File.Exists(target));
        Assert.Equal("created content", await File.ReadAllTextAsync(target));
        Assert.Empty(Directory.GetDirectories(_tempRoot, CharacterTagFileTransaction.DirectoryPrefix + "*"));
    }

    [Fact]
    public async Task CommitAsync_RejectsPathOutsideRoot()
    {
        string outside = Path.Combine(Path.GetTempPath(), "bdtm-outside-" + Guid.NewGuid().ToString("N") + ".txt");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CharacterTagFileTransaction.CommitAsync(
                _tempRoot,
                new[] { new CharacterTagFileChange(outside, "should not write") }));

        Assert.False(File.Exists(outside));
        Assert.Empty(Directory.GetDirectories(_tempRoot, CharacterTagFileTransaction.DirectoryPrefix + "*"));
    }
}
