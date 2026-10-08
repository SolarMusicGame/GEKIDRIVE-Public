using System;
using System.Collections.Generic;
using System.IO;
using GekiDrive;

class Program
{
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    static int Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "GekiDrive-cache-tests-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(root, "analysis.bin");
        try
        {
            var records = new List<AnalysisRecord> {
                new AnalysisRecord { Key=1002, Exists=true, Bpm=180, Platinum=1500, Designer="測試 / Designer" },
                new AnalysisRecord { Key=1004, Exists=false, Bpm=0, Platinum=0, Designer="" }
            };
            var keys = new HashSet<int> {1002, 1004};
            List<AnalysisRecord> loaded;
            Require(!AnalysisCacheStore.TryRead(path, "manifest-a", keys, out loaded), "Missing cache must fall back");
            AnalysisCacheStore.Write(path, "manifest-a", records);
            Require(AnalysisCacheStore.TryRead(path, "manifest-a", keys, out loaded), "Cache must restore valid snapshot");
            Require(loaded.Count==2 && loaded[0].Designer==records[0].Designer && loaded[0].Platinum==1500 && !loaded[1].Exists, "All game metadata must survive cache roundtrip");
            Require(!AnalysisCacheStore.TryRead(path, "manifest-b", keys, out loaded), "Changed chart manifest must invalidate cache");
            Require(!AnalysisCacheStore.TryRead(path, "manifest-a", new HashSet<int>{1002,1005}, out loaded), "Same-size changed song/difficulty IDs must invalidate cache");
            Require(!AnalysisCacheStore.TryRead(path, "manifest-a", new HashSet<int>{1002}, out loaded), "Removed chart must invalidate cache");
            byte[] good = File.ReadAllBytes(path);
            byte[] corrupt = (byte[])good.Clone(); corrupt[corrupt.Length / 2] ^= 1; File.WriteAllBytes(path, corrupt);
            Require(!AnalysisCacheStore.TryRead(path, "manifest-a", keys, out loaded), "Corrupt payload must be rejected before restoring scores");
            File.WriteAllBytes(path, new byte[]{1,2,3});
            Require(!AnalysisCacheStore.TryRead(path, "manifest-a", keys, out loaded), "Truncated cache must fall back");
            records[0].Bpm=200;
            AnalysisCacheStore.Write(path, "manifest-new", records);
            Require(AnalysisCacheStore.TryRead(path, "manifest-new", keys, out loaded) && loaded[0].Bpm==200, "Rebuilding must atomically replace old cache");
            records[1].Key=1002;
            AnalysisCacheStore.Write(path, "manifest-duplicate", records);
            Require(!AnalysisCacheStore.TryRead(path, "manifest-duplicate", keys, out loaded), "Duplicate chart entries must not restore incomplete dictionary");
            Console.WriteLine("PASS: cache preserves metadata, invalidates changed manifests/IDs, rejects corruption/truncation/duplicates, and replaces snapshots.");
            return 0;
        }
        finally
        {
            // Only delete files directly created in this isolated test directory.
            if (Directory.Exists(root)) { foreach (string file in Directory.GetFiles(root)) File.Delete(file); Directory.Delete(root); }
        }
    }
}

