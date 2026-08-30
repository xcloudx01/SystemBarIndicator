using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace SystemBarIndicator.Tests
{
    /// <summary>
    /// Guards the repository rule that executable PowerShell stays ASCII-only: a BOM-less
    /// UTF-8 script re-read by powershell.exe under the ANSI code page gets mangled, which
    /// once registered the scheduled task under a mojibake folder name. Checked here rather
    /// than by eye, because a single pasted dash is enough to break it.
    /// </summary>
    public class ScriptEncodingTests
    {
        public static TheoryData<string> PowerShellScripts()
        {
            var data = new TheoryData<string>();
            foreach (var path in Directory.GetFiles(Path.Combine(RepositoryRoot(), "deploy"), "*.ps1"))
                data.Add(Path.GetFileName(path));
            return data;
        }

        [Theory]
        [MemberData(nameof(PowerShellScripts))]
        public void DeployScriptsAreAsciiOnly(string fileName)
        {
            string path = Path.Combine(RepositoryRoot(), "deploy", fileName);
            string[] lines = File.ReadAllLines(path);

            var offenders = new List<string>();
            for (int i = 0; i < lines.Length; i++)
            {
                foreach (char c in lines[i])
                {
                    if (c > 0x7F)
                    {
                        offenders.Add($"{fileName}:{i + 1} contains U+{(int)c:X4} '{c}'");
                        break;
                    }
                }
            }

            Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
        }

        [Fact]
        public void TheGuardActuallySeesTheScripts()
        {
            // A silently empty file list would make the test above pass forever.
            Assert.NotEmpty(Directory.GetFiles(Path.Combine(RepositoryRoot(), "deploy"), "*.ps1"));
        }

        private static string RepositoryRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SystemBarIndicator.sln")))
                dir = dir.Parent;

            Assert.NotNull(dir);
            return dir!.FullName;
        }
    }
}
