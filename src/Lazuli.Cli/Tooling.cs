using System.Diagnostics;

namespace Lazuli.Cli;

// Thin wire over the .NET tooling: lazuli verbs carry the blessed defaults, but the engines are
// dotnet's own (test, stryker). We invoke `dotnet <command>` inheriting the current directory and
// stdio, and return its exit code — no reimplementation, just opinionated invocation.
internal static class Tooling
{
    public static int Dotnet(string command, string[] args)
    {
        var info = new ProcessStartInfo("dotnet") { UseShellExecute = false };
        info.ArgumentList.Add(command);
        foreach (var arg in args)
            info.ArgumentList.Add(arg);

        using var process = Process.Start(info);
        if (process is null)
        {
            Console.Error.WriteLine($"lazuli: could not start 'dotnet {command}'.");
            return 1;
        }

        process.WaitForExit();
        return process.ExitCode;
    }
}
