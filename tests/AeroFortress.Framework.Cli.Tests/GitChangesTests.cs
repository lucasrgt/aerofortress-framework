using System.Diagnostics;
using AeroFortress.Framework.Cli;

namespace AeroFortress.Framework.Cli.Tests;

public sealed class GitChangesTests
{
    [Fact]
    public void Staged_mode_reads_the_index_without_borrowing_unstaged_files()
    {
        var root = Repository();
        try
        {
            Write(root, "staged.cs", "staged");
            Git(root, "add", "staged.cs");
            Write(root, "tracked.cs", "unstaged");

            var changes = GitChanges.Read(root, new GateOptions(GateMode.Staged, true, null, []));

            Assert.True(changes.Reliable);
            Assert.Equal(["staged.cs"], changes.Files);
        }
        finally
        {
            DeleteRepository(root);
        }
    }

    [Fact]
    public void Affected_mode_includes_committed_working_and_untracked_changes()
    {
        var root = Repository();
        try
        {
            Write(root, "tracked.cs", "working");
            Write(root, "new.cs", "untracked");

            var changes = GitChanges.Read(root, new GateOptions(GateMode.Affected, false, "HEAD", []));

            Assert.True(changes.Reliable);
            Assert.Equal(["new.cs", "tracked.cs"], changes.Files);
        }
        finally
        {
            DeleteRepository(root);
        }
    }

    [Fact]
    public async Task Working_tree_discovery_drains_large_git_stderr_without_deadlocking()
    {
        var root = Repository();
        try
        {
            Git(root, "config", "core.autocrlf", "true");
            Git(root, "config", "core.safecrlf", "warn");
            Directory.CreateDirectory(Path.Combine(root, "warnings"));
            for (var index = 0; index < 768; index++)
                Write(root, $"warnings/long-conversion-warning-file-{index:D4}.cs", "baseline\r\n");
            Git(root, "add", "warnings");
            Git(root, "commit", "-q", "-m", "warning baseline");

            for (var index = 0; index < 768; index++)
                Write(root, $"warnings/long-conversion-warning-file-{index:D4}.cs", "changed\n");

            var read = Task.Run(() => GitChanges.Read(
                root,
                new GateOptions(GateMode.Affected, false, "HEAD", [])));

            var changes = await read.WaitAsync(TimeSpan.FromSeconds(30));

            Assert.True(changes.Reliable);
            Assert.Equal(768, changes.Files.Count);
        }
        finally
        {
            DeleteRepository(root);
        }
    }

    private static string Repository()
    {
        var root = Directory.CreateTempSubdirectory("af-git-changes-").FullName;
        Git(root, "init", "-q");
        Git(root, "config", "user.email", "gate@example.test");
        Git(root, "config", "user.name", "Gate Test");
        Write(root, "tracked.cs", "baseline");
        Git(root, "add", "tracked.cs");
        Git(root, "commit", "-q", "-m", "baseline");
        return root;
    }

    private static void Git(string root, params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { UseShellExecute = false, WorkingDirectory = root };
        foreach (var argument in arguments)
            info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
    }

    private static void Write(string root, string relativePath, string content) =>
        File.WriteAllText(Path.Combine(root, relativePath), content);

    private static void DeleteRepository(string root)
    {
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(root, recursive: true);
    }
}
