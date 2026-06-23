using IWshRuntimeLibrary;
using MayoUtilities.Classes;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection; 
using System.Text;
using System.Threading.Tasks; 
using File = System.IO.File;  // add COM reference (see below)

namespace BASM.Classes {
    public class PathSolver {
        public static bool IsPathRelative(string path) => !(path.Length > 1 && path[1] != ':');
        public static string GetRelativePath(string expectedDir, string path) {
            if (string.IsNullOrEmpty(path)) return expectedDir;

            var parts = new List<string>(path.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries));

            if (parts[0].Length > 1 && parts[0][1] != ':' && expectedDir.Length>0)
                return GetRelativePath("", Path.Combine(expectedDir, path));

            for (int i = 1; i < parts.Count; i++)                 if (parts[i] == "...") {
                    parts.RemoveAt(i);
                    parts.RemoveAt(i - 1);
                    i--;
                    i--;
                }

            return string.Join("\\", parts);
        }
        public static string ToRelativePath(string directory, string path) {
            if (string.IsNullOrEmpty(directory)) return path;
            if (string.IsNullOrEmpty(path)) return path;

            // Normalize and split
            var dirParts = directory.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            var fileParts = path.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);

            int ci = 0;
            int minLength = Math.Min(dirParts.Length, fileParts.Length);

            // 1. Find the point where they diverge (Case Insensitive)
            while (ci < minLength &&
                   string.Equals(dirParts[ci], fileParts[ci]))                 ci++;

            // If they have nothing in common, return the absolute path
            //if (ci == 0) return path;

            var resultParts = new List<string>();

            // 2. For every level in 'directory' that is NOT in 'path', add a ".."
            // This handles the "climbing up" logic.
            for (int i = ci; i < dirParts.Length; i++)                 resultParts.Add("..");

            // 3. Add the remaining segments of the destination path
            for (int i = ci; i < fileParts.Length; i++)                 resultParts.Add(fileParts[i]);

            // If the path was identical, return "." for "Current Directory"
            if (resultParts.Count == 0) return ".";

            return string.Join("\\", resultParts);
        }
        public static string GetAppDataFolder() {
            // Example: C:\Users\<User>\AppData\Roaming\MayoWallpaperEngine\settings.txt
            string appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                AppName
            );
            Directory.CreateDirectory(appDataDir); // Ensure it exists
            return appDataDir;
        }
        /// <summary>
        /// If doesn't exists , it creates a new file app data pf application
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static string CreateAppDataFile(string fileName) {

            fileName = Path.Combine(GetAppDataFolder(), fileName);
            if (!File.Exists(fileName)) File.Create(fileName).Close();
            return fileName;
        }


        public static string exePathFile = AppDomain.CurrentDomain.BaseDirectory + "exe.txt";
        public static string BaseAppDir = AppDomain.CurrentDomain.BaseDirectory; 
        public static string AppName = "BASM";
        public static string SharedLibs = "Shared_Libs";
        static PathSolver() {
            string exePath = "";
            if (File.Exists(exePathFile)) exePath = File.ReadAllLines(exePathFile)[0];
            BaseAppDir = GetRelativePath(AppDomain.CurrentDomain.BaseDirectory, exePath);


            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) => {
                Debugger.Info("Resolving assembly: " + args.Name);

                string assemblyName = new AssemblyName(args.Name).Name + ".dll";
                string assemblyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, assemblyName);
                if (File.Exists(assemblyPath)) return Assembly.LoadFrom(assemblyPath);

                string folder = Path.Combine(BaseAppDir,SharedLibs);
                assemblyPath = Path.Combine(folder, assemblyName);
                if (File.Exists(assemblyPath)) return Assembly.LoadFrom(assemblyPath);

                Debugger.Error("Resolving assembly Failed: " + assemblyPath);
                return null;
            }; 
             
        }
        public static void SetAppName() => AppName = Assembly.GetEntryAssembly()?.GetName().Name ?? AppName; 
         
        public static string GetRelativePath(string path) => GetRelativePath(BaseAppDir, path);
         
        // Make sure to call this method with 'await' in an async context
        public static void DownloadFile(string fileUrl, string destinationPath) {
            using (var webClient = new WebClient())                 try {
                    // Download the file synchronously
                    webClient.DownloadFile(fileUrl, destinationPath);
                } catch (WebException e) {
                    // Handle HTTP 404, network errors, etc.
                    Console.WriteLine($"Download failed: {e.Message}");
                    throw;
                }
        } 

        public static Progress progress = new Progress();

        struct BufInfo {
            public byte[] Buf;
            public int Count;
            }; 
        public static async Task DownloadFileAsync(string url, string path) {
            Debugger.Log($"Downloading {url} to\n {path}");
            // BlockingCollection is the .NET 4.7.2 equivalent of a conveyor belt

            progress.Reset();
            progress.Start();

            using (var httpClient = new HttpClient())                 using (var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead)) {
                    response.EnsureSuccessStatusCode();

                    Debugger.Log("Downloading " + url);
                    progress.Total = response.Content.Headers.ContentLength ?? -1;

                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true)) {
                        var buffer = new byte[16384];
                        int read;
                        var bufs = new Queue<BufInfo>();

                        bool done = false;
                        _ = Task.Run(async () => {
                            while (true)                                 if (bufs.Count > 0) {
                                    var buf = bufs.Dequeue();
                                    await file.WriteAsync(buf.Buf, 0, buf.Count);
                                } else {
                                    if (done) break;
                                    await Task.Delay(16);
                                }
                        });
                        while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0) {
                            bufs.Enqueue(new BufInfo() { Buf = buffer, Count = read });
                            buffer = new byte[16384];
                            progress.Done += read;  
                        }
                        while (bufs.Count > 0) await Task.Delay(16);
                        Debugger.Info("Downloaded " + (file.Length >> 10) + " KBs");
                        done = true;
                    }
                }
            progress.End();
        }
        public static async Task<string> DownloadAsync(string url) {
            progress.Reset();
            progress.Start();

            using (var httpClient = new HttpClient())                 using (var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead)) {
                    response.EnsureSuccessStatusCode();

                    progress.Total = response.Content.Headers.ContentLength ?? -1;

                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var ms = new MemoryStream()) {
                        var buffer = new byte[64 << 10];
                        int read;

                        while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0) {
                            await ms.WriteAsync(buffer, 0, read);
                            progress.Done += read;
                        }
                        Debugger.Info("Downloaded " + ms.Length + " Bytes");

                        progress.End();
                        ms.Position = 0L;

                        var charset = response.Content.Headers.ContentType?.CharSet;
                        var encoding = string.IsNullOrEmpty(charset) ? Encoding.UTF8 : Encoding.GetEncoding(charset);

                        using (var sr = new StreamReader(ms, encoding))                             return sr.ReadToEnd();
                    }
                }
        }
        public static string CreateTempFile(string filename, string appName = "MayoWallpaperEngine") {
            string dir = Path.Combine(Path.GetTempPath(), appName);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            filename = Path.Combine(dir, filename);
            if (!File.Exists(filename)) File.Create(filename).Close();
            return filename;
        }

        public static void Test() {
            Debugger.Log(GetRelativePath(BaseAppDir, ".../"));
            Debugger.Log(GetRelativePath(BaseAppDir, ".../.../"));
            Debugger.Log(GetRelativePath(BaseAppDir, "Mayo"));

            Debugger.Log(ToRelativePath("mayo/test/test1/test2", "Mayo"));
            Debugger.Log(ToRelativePath("mayo/test/test1/test2", "mayo/Mayo"));
            Debugger.Log(ToRelativePath("mayo/test/test1/test2", "mayo/test/Mayo"));
        }
    }
}
