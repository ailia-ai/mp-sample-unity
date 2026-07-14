#if UNITY_STANDALONE_LINUX

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Debug = UnityEngine.Debug;

namespace SFB {

    // Linux implementation.
    //
    // The bundled prebuilt libStandaloneFileBrowser.so loads GTK3 into the Unity
    // process and corrupts the heap while tearing down the file-chooser dialog
    // (free(): invalid pointer inside g_object_run_dispose / gtk_tree_view_remove_column)
    // on recent distributions. To avoid loading GTK in-process at all, we shell out
    // to an external dialog helper (zenity, with a kdialog fallback) instead.
    public class StandaloneFileBrowserLinux : IStandaloneFileBrowser {

        private enum Backend { Zenity, Kdialog, None }

        private static readonly Backend _backend = DetectBackend();

        public StandaloneFileBrowserLinux() {
            if (_backend == Backend.None) {
                Debug.LogError("[SFB] No file dialog helper found. Install 'zenity' (or 'kdialog').");
            }
        }

        // ---- IStandaloneFileBrowser (sync) ----

        public string[] OpenFilePanel(string title, string directory, ExtensionFilter[] extensions, bool multiselect) {
            var args = new List<string>();
            if (_backend == Backend.Zenity) {
                args.Add("--file-selection");
                AddZenityCommon(args, title, directory, null, multiselect, false);
                AddZenityFilters(args, extensions);
            }
            else {
                args.Add("--getopenfilename");
                if (multiselect) { args.Add("--multiple"); args.Add("--separate-output"); }
                AddKdialogCommon(args, title);
                args.Add(StartPath(directory, null));
                args.Add(KdialogFilter(extensions));
            }
            return Run(args, multiselect);
        }

        public string[] OpenFolderPanel(string title, string directory, bool multiselect) {
            var args = new List<string>();
            if (_backend == Backend.Zenity) {
                args.Add("--file-selection");
                args.Add("--directory");
                AddZenityCommon(args, title, directory, null, multiselect, false);
            }
            else {
                args.Add("--getexistingdirectory");
                AddKdialogCommon(args, title);
                args.Add(StartPath(directory, null));
            }
            return Run(args, multiselect);
        }

        public string SaveFilePanel(string title, string directory, string defaultName, ExtensionFilter[] extensions) {
            var args = new List<string>();
            if (_backend == Backend.Zenity) {
                args.Add("--file-selection");
                args.Add("--save");
                args.Add("--confirm-overwrite");
                AddZenityCommon(args, title, directory, defaultName, false, true);
                AddZenityFilters(args, extensions);
            }
            else {
                args.Add("--getsavefilename");
                AddKdialogCommon(args, title);
                args.Add(StartPath(directory, defaultName));
                args.Add(KdialogFilter(extensions));
            }
            var result = Run(args, false);
            return result.Length > 0 ? result[0] : "";
        }

        // ---- IStandaloneFileBrowser (async) ----
        // NOTE: the callback is invoked from a background thread. Callers that touch
        // Unity APIs must marshal back to the main thread themselves.

        public void OpenFilePanelAsync(string title, string directory, ExtensionFilter[] extensions, bool multiselect, Action<string[]> cb) {
            RunAsync(() => OpenFilePanel(title, directory, extensions, multiselect), cb);
        }

        public void OpenFolderPanelAsync(string title, string directory, bool multiselect, Action<string[]> cb) {
            RunAsync(() => OpenFolderPanel(title, directory, multiselect), cb);
        }

        public void SaveFilePanelAsync(string title, string directory, string defaultName, ExtensionFilter[] extensions, Action<string> cb) {
            RunAsync(() => SaveFilePanel(title, directory, defaultName, extensions), cb);
        }

        // ---- helpers ----

        private static Backend DetectBackend() {
            if (CommandExists("zenity")) return Backend.Zenity;
            if (CommandExists("kdialog")) return Backend.Kdialog;
            return Backend.None;
        }

        private static bool CommandExists(string cmd) {
            try {
                var psi = new ProcessStartInfo {
                    FileName = "/usr/bin/env",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                psi.ArgumentList.Add("which");
                psi.ArgumentList.Add(cmd);
                using (var p = Process.Start(psi)) {
                    p.WaitForExit();
                    return p.ExitCode == 0;
                }
            }
            catch (Exception) {
                return false;
            }
        }

        // Runs the helper and returns selected paths. Zero-length array when cancelled or on error.
        private static string[] Run(List<string> args, bool multiselect) {
            if (_backend == Backend.None) {
                return new string[0];
            }

            var psi = new ProcessStartInfo {
                FileName = _backend == Backend.Zenity ? "zenity" : "kdialog",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (var a in args) {
                psi.ArgumentList.Add(a);
            }

            try {
                using (var p = Process.Start(psi)) {
                    string stdout = p.StandardOutput.ReadToEnd();
                    p.WaitForExit();
                    // zenity/kdialog: exit 0 = selected, 1 = cancelled, others = error.
                    if (p.ExitCode != 0) {
                        return new string[0];
                    }
                    stdout = stdout.TrimEnd('\r', '\n');
                    if (string.IsNullOrEmpty(stdout)) {
                        return new string[0];
                    }
                    return multiselect
                        ? stdout.Split('\n')
                        : new[] { stdout };
                }
            }
            catch (Exception e) {
                Debug.LogError("[SFB] Failed to launch file dialog: " + e);
                return new string[0];
            }
        }

        private static void RunAsync<T>(Func<T> work, Action<T> cb) {
            var thread = new Thread(() => {
                var result = work();
                cb?.Invoke(result);
            });
            thread.IsBackground = true;
            thread.Start();
        }

        private static void AddZenityCommon(List<string> args, string title, string directory, string defaultName, bool multiselect, bool isSave) {
            if (!string.IsNullOrEmpty(title)) {
                args.Add("--title=" + title);
            }
            var filename = BuildFilename(directory, defaultName, isSave);
            if (!string.IsNullOrEmpty(filename)) {
                args.Add("--filename=" + filename);
            }
            if (multiselect) {
                args.Add("--multiple");
                args.Add("--separator=\n");
            }
        }

        private static void AddZenityFilters(List<string> args, ExtensionFilter[] extensions) {
            if (extensions == null) {
                return;
            }
            foreach (var filter in extensions) {
                var patterns = new List<string>();
                if (filter.Extensions != null) {
                    foreach (var ext in filter.Extensions) {
                        patterns.Add(ExtToGlob(ext));
                    }
                }
                if (patterns.Count == 0) {
                    continue;
                }
                var name = string.IsNullOrEmpty(filter.Name) ? "Files" : filter.Name;
                args.Add("--file-filter=" + name + " | " + string.Join(" ", patterns));
            }
        }

        private static void AddKdialogCommon(List<string> args, string title) {
            if (!string.IsNullOrEmpty(title)) {
                args.Add("--title");
                args.Add(title);
            }
        }

        // kdialog filter: "*.png *.jpg|Image Files\n*|All Files"
        private static string KdialogFilter(ExtensionFilter[] extensions) {
            if (extensions == null) {
                return "";
            }
            var groups = new List<string>();
            foreach (var filter in extensions) {
                var patterns = new List<string>();
                if (filter.Extensions != null) {
                    foreach (var ext in filter.Extensions) {
                        patterns.Add(ExtToGlob(ext));
                    }
                }
                if (patterns.Count == 0) {
                    continue;
                }
                var name = string.IsNullOrEmpty(filter.Name) ? "Files" : filter.Name;
                groups.Add(string.Join(" ", patterns) + "|" + name);
            }
            return string.Join("\n", groups);
        }

        // Normalize "png", "*.png" or ".png" to a "*.png" glob. "*" stays "*".
        private static string ExtToGlob(string ext) {
            if (string.IsNullOrEmpty(ext) || ext == "*" || ext == "*.*") {
                return "*";
            }
            if (ext.StartsWith("*.")) {
                return ext;
            }
            return "*." + ext.TrimStart('.');
        }

        private static string BuildFilename(string directory, string defaultName, bool isSave) {
            var dir = directory ?? "";
            if (dir.Length > 0 && !dir.EndsWith("/")) {
                dir += "/";
            }
            if (isSave && !string.IsNullOrEmpty(defaultName)) {
                return dir + defaultName;
            }
            return dir;
        }

        private static string StartPath(string directory, string defaultName) {
            if (!string.IsNullOrEmpty(directory)) {
                var dir = directory.EndsWith("/") ? directory : directory + "/";
                return string.IsNullOrEmpty(defaultName) ? dir : dir + defaultName;
            }
            var home = Environment.GetEnvironmentVariable("HOME");
            var basePath = string.IsNullOrEmpty(home) ? "." : home;
            return string.IsNullOrEmpty(defaultName) ? basePath + "/" : basePath + "/" + defaultName;
        }
    }
}

#endif
