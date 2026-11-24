using Kmila.Shared.Data;
using Kmila.Shared.Models;
using Kmila.Shared.Services;

namespace Kmila.Shared.Tests;

// Retention sweep of SimulationSnapshotStore (per-project LRU cap on unpinned runs).
// Phase 2 replaced the sweep's "load every unpinned row incl. its WaveformJson, then
// Skip(MaxPerProject)" with a single SQL DELETE; these tests pin the semantics:
// keep the newest MaxPerProject unpinned rows per project, never evict pinned rows,
// never touch other projects.
public class SimulationSnapshotRetentionTests
{
    [Fact]
    public async Task Retention_KeepsNewestUnpinned_NeverEvictsPinned_OrOtherProjects()
    {
        var path = Path.Combine(Path.GetTempPath(), $"kmila_snap_{Guid.NewGuid():N}.db");
        var db = new ApplicationDbContext(path);
        try
        {
            var store = new SimulationSnapshotStore(db);
            var wf = WaveformData.CreateEmpty(Interpreter.Models.PhysicalTime.DefaultTickFs);
            const int project = 1, other = 2;

            // 3 old runs, pinned.
            var pinnedIds = new List<int>();
            for (int i = 0; i < 3; i++)
            {
                var s = await store.CaptureAsync(project, wf, 100, "ns", null, $"pinned-{i}");
                Assert.NotNull(s);
                Assert.True(await store.SetPinnedAsync(s!.Id, true));
                pinnedIds.Add(s.Id);
            }

            // A different project's runs must be unaffected.
            for (int i = 0; i < 2; i++)
                Assert.NotNull(await store.CaptureAsync(other, wf, 100, "ns", null, $"other-{i}"));

            // MaxPerProject + 5 unpinned runs → the 5 oldest unpinned must go.
            int extra = 5;
            SimulationSnapshot? newest = null;
            var firstUnpinned = new List<int>();
            for (int i = 0; i < SimulationSnapshotStore.MaxPerProject + extra; i++)
            {
                newest = await store.CaptureAsync(project, wf, 100, "ns", null, $"run-{i}");
                Assert.NotNull(newest);
                if (i < extra) firstUnpinned.Add(newest!.Id);
                await Task.Delay(1); // distinct CreatedAt so "oldest" is well defined
            }

            var rows = await store.ListMetadataAsync(project);
            Assert.Equal(SimulationSnapshotStore.MaxPerProject, rows.Count(r => !r.Pinned));
            Assert.Equal(3, rows.Count(r => r.Pinned));
            Assert.All(pinnedIds, id => Assert.Contains(rows, r => r.Id == id));
            Assert.Contains(rows, r => r.Id == newest!.Id);
            Assert.All(firstUnpinned, id => Assert.DoesNotContain(rows, r => r.Id == id));

            Assert.Equal(2, (await store.ListMetadataAsync(other)).Count);
        }
        finally
        {
            await db.CloseAsync();
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }
}
