using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace MobaTrainer.EditorTools
{
    /// <summary>
    /// One-click GitHub sync: Tools > MOBA Trainer > Push to GitHub.
    /// First run: git init, set the remote, first commit, push. Later runs: commit changes and push.
    /// Runs git in the background and reports every step in the Console. Needs Git for Windows
    /// (its credential manager opens a browser sign-in the first time).
    /// </summary>
    public static class MobaTrainerGit
    {
        private const string Remote = "https://github.com/alperyayla1/Moba-Trainer.git";
        private const string Branch = "main";
        private static bool busy;

        [MenuItem("Tools/MOBA Trainer/Push to GitHub", priority = 20)]
        public static void Push()
        {
            if (busy)
            {
                Debug.Log("[Git] Already pushing, please wait.");
                return;
            }
            AssetDatabase.SaveAssets();
            string root = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            string message = "Update " + DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            busy = true;
            Debug.Log("[Git] Pushing project to " + Remote + " ...");
            Task.Run(() =>
            {
                try { Run(root, message); }
                catch (Exception e) { Debug.LogError("[Git] " + e.Message); }
                finally { busy = false; }
            });
        }

        private static void Run(string root, string message)
        {
            if (Git(root, "--version", out string version) != 0)
            {
                Debug.LogError("[Git] Git is not installed. Install it from https://git-scm.com/download/win (default options), restart Unity, then use Tools > MOBA Trainer > Push to GitHub again.");
                return;
            }
            Debug.Log("[Git] " + version.Trim());

            if (!Directory.Exists(Path.Combine(root, ".git")))
            {
                Must(root, "init");
                Must(root, "checkout -B " + Branch);
            }

            // Identity for commits (only if none is configured). Uses GitHub's private no-reply address.
            if (Git(root, "config user.name", out string name) != 0 || string.IsNullOrWhiteSpace(name))
            {
                Must(root, "config user.name \"alperyayla1\"");
            }
            if (Git(root, "config user.email", out string mail) != 0 || string.IsNullOrWhiteSpace(mail))
            {
                Must(root, "config user.email \"alperyayla1@users.noreply.github.com\"");
            }

            if (Git(root, "remote get-url origin", out _) != 0) Must(root, "remote add origin " + Remote);
            else Must(root, "remote set-url origin " + Remote);

            Must(root, "add -A");
            Git(root, "status --porcelain", out string status);
            if (!string.IsNullOrWhiteSpace(status))
            {
                Must(root, "commit -m \"" + message + "\"");
            }
            else
            {
                Debug.Log("[Git] Nothing new to commit.");
            }

            Git(root, "rev-parse --abbrev-ref HEAD", out string head);
            head = head.Trim();
            if (string.IsNullOrEmpty(head) || head == "HEAD") head = Branch;

            if (Git(root, "push -u origin " + head, out string push) != 0)
            {
                Debug.LogError("[Git] Push failed:\n" + push +
                               "\nIf a GitHub sign-in window appeared, finish it and run Push to GitHub again.");
                return;
            }
            Debug.Log("[Git] Done. https://github.com/alperyayla1/Moba-Trainer\n" + push);
        }

        private static void Must(string root, string args)
        {
            if (Git(root, args, out string output) != 0)
            {
                throw new Exception("git " + args + " failed:\n" + output);
            }
        }

        private static int Git(string root, string args, out string output)
        {
            var psi = new ProcessStartInfo("git", args)
            {
                WorkingDirectory = root,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            psi.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";   // never hang waiting for typed input
            try
            {
                using (Process p = Process.Start(psi))
                {
                    var sb = new StringBuilder();
                    Task<string> err = p.StandardError.ReadToEndAsync();
                    sb.Append(p.StandardOutput.ReadToEnd());
                    p.WaitForExit(10 * 60 * 1000);
                    sb.Append(err.Result);
                    output = sb.ToString();
                    return p.HasExited ? p.ExitCode : -1;
                }
            }
            catch (Exception e)
            {
                output = e.Message;
                return -1;
            }
        }
    }
}
