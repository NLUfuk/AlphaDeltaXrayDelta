using System.Text;
using CrmKanban.Infrastructure.Files;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace CrmKanban.Api.Tests;

/// <summary>Local-disk storage: bytes round-trip, and the filesystem trust boundary holds
/// (a traversal key can't escape the root).</summary>
public class LocalFileStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "crm-local-test-" + Guid.NewGuid().ToString("N"));
    private LocalFileStorage Create() => new(Options.Create(new LocalStorageOptions { RootPath = _root }));

    [Fact]
    public async Task Put_then_get_round_trips_the_bytes()
    {
        var storage = Create();
        var key = $"{Guid.NewGuid():N}/{Guid.NewGuid():N}/hello.txt";
        var payload = Encoding.UTF8.GetBytes("merhaba dünya");

        await storage.PutAsync(key, new MemoryStream(payload), "text/plain");
        await using var read = await storage.GetAsync(key);
        using var buffer = new MemoryStream();
        await read.CopyToAsync(buffer);

        buffer.ToArray().Should().Equal(payload);
    }

    [Fact]
    public async Task Traversal_key_is_rejected()
    {
        var storage = Create();
        var escape = () => storage.PutAsync("../../escape.txt", new MemoryStream([1, 2, 3]), "text/plain");
        await escape.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }
}
