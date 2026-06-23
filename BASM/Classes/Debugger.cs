using System;
using System.IO;
using System.Runtime.InteropServices;

namespace BASM.Classes {
    public static class Debugger {
        /* 
#if DEBUG
            Debugger.DEBUG = true;
#else
            Debugger.DEBUG = false;
#endif
        */
        public static bool DEBUG = true; 
        [DllImport("kernel32.dll")]
        static extern bool AllocConsole();
        [DllImport("kernel32.dll")]
        static extern bool AttachConsole(int dwProcessId);
        private const int ATTACH_PARENT_PROCESS = -1;

        public enum DialogResult {
            None = 0,
            OK = 1,
            Cancel = 2,
            Yes = 6,
            No = 7,
        }
        public enum MessageBoxButtons {
            OK = 0,
            YesNo = 1,
        }
        public enum MessageBoxIcon {
            None = 0,
            Information = 1,
            Warning = 2,
            Error = 3,
            Exclamation = 4,
        }
        static DialogResult MessageBoxShow(
            string msg = "",
            string caption = "MayoMessageBox",
            MessageBoxButtons buttons = MessageBoxButtons.OK,
            MessageBoxIcon icon = MessageBoxIcon.None) {
            // MessageBox.Show(message,caption);
            //return MayoWinForms.Forms.MayoMessageBox.Show(msg, caption, buttons, icon);
            return DialogResult.OK; //ShowMessageBox.Invoke(msg, caption, buttons, icon);
        }


        //public static Func<string, string, MessageBoxButtons, MessageBoxIcon, DialogResult> ShowMessageBox
        //    = (text, cpt, btn, ico) => MessageBox.Show(text, cpt, btn, ico);
        public static void ShowConsole() {
            if (File.Exists("__mayo_debug_override.config")) DEBUG = true;
            if (DEBUG) AllocConsole();
            else AttachConsole(ATTACH_PARENT_PROCESS);
            if (DEBUG) StartLogging();
            else CreateLogFile();
             
            // Catch non-UI thread exceptions
            AppDomain.CurrentDomain.UnhandledException += (sender, ex) => {
                // 1. Get the actual Exception object by casting ExceptionObject
                var actualException = ex.ExceptionObject as Exception;

                // 2. Check if the cast was successful and the exception object exists
                var msg = $"Non-UI Exception:\n{ex.ExceptionObject}";
                if (actualException != null)                     // 3. Construct the message using the detailed properties
                     msg = $"Non-UI Exception Occurred:\n" +
                              $"Type: {actualException.GetType().FullName}\n" +
                              $"Message: {actualException.Message}\n" +
                              $"Stack Trace:\n{actualException.StackTrace}";

                StopLogging();
                Error(msg);
                // Ensure this write is thread-safe (as discussed in the previous response)
                File.AppendAllText(LogFile, msg + Environment.NewLine + "---" + Environment.NewLine);
               // MessageBox.Show(msg, "Unhandled Background Exception", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
        }
        public static void Run(Action run) {
            try {
                run.Invoke();
                StopLogging();
            }
            // This filter catches errors in RELEASE but lets the debugger catch them natively in DEBUG
            catch (Exception e) when (!DEBUG) {
                var msg = "Unhandled exception occured!\nCheck logs for more info!\nException : " + e.ToString();
                Error(msg);
                StopLogging();
                File.AppendAllText(LogFile, msg);
            }

            if (DEBUG) {
                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
            }
        }
        public static void Test() {
            Log("Log");
            Info("Info");
            Warn("Warn");
            Error("Error");
            MessageBoxShow("Hello world");
        }
        static FileStream logS = null;
        static string LogFile = "logs.txt";
        private static void StartLogging() {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LogFile);

            if (TryCreateLogStream(path)) goto _done;
            Warn("Logging failed at " + path);

            path = PathSolver.CreateAppDataFile(LogFile);
            if (TryCreateLogStream(path)) goto _done;
            Warn("Logging failed at " + path);

            path = Path.Combine(Path.GetTempPath(), LogFile);
            if (TryCreateLogStream(path)) goto _done;
            Warn("Logging failed at " + path);

            Error("Logging failed!");
            return;
        _done:
            {
                LogFile = path;
                Info("Log Started at :" + LogFile);
            }
        }
        private static void CreateLogFile() {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LogFile);

            if (TryCreateLogStream(path)) goto _done;
            Warn("Logging failed at " + path);

            path = PathSolver.CreateAppDataFile(LogFile);
            if (TryCreateLogStream(path)) goto _done;
            Warn("Logging failed at " + path);

            path = Path.Combine(Path.GetTempPath(), LogFile);
            if (TryCreateLogStream(path)) goto _done;
            Warn("Logging failed at " + path);

            Error("Logging failed!");
            return;
        _done:
            {
                LogFile = path;
                Info("Log File created at :" + LogFile);
                StopLogging();
            }
        }
        private static void StopLogging() {
            if (logS == null) return;
            logS.Close();
            logS.Dispose();
            logS = null;
        }




        private static bool TryCreateLogStream(string path) {
            try {
                // Ensure directory exists for the path provided
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))                     Directory.CreateDirectory(directory);

                logS = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
                return true;
            } catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException) {
                // Silently fail to allow the next fallback path to try
                return false;
            }
        }
        static void WriteColored(Action Write, ConsoleColor color) {
            var original = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Write();
            Console.ForegroundColor = original;
        }
        static void WriteLine(string message, params string[] args) => Write(message + "\r\n", args);
        static void Write(string message, params string[] args) {
            if (args == null || args.Length == 0) {
                // Simple path if there are no arguments to colorize
                Console.Write(message);
            } else {
                // Split the message by the format placeholders, e.g., {0}, {1}
                // This regex captures the placeholders so they are included in the split array
                string[] parts = System.Text.RegularExpressions.Regex.Split(message, @"(\{\d+\})");

                foreach (var part in parts) {
                    // Check if the part is a placeholder like "{0}"
                    if (part.StartsWith("{") && part.EndsWith("}") && int.TryParse(part.Substring(1, part.Length - 2), out int index)) {
                        if (index >= 0 && index < args.Length) {
                            // Change color to Magenta for the argument
                            ConsoleColor originalColor = Console.ForegroundColor;
                            Console.ForegroundColor = ConsoleColor.Magenta;
                            Console.Write(args[index]);
                            Console.ForegroundColor = originalColor;
                        } else {
                            // Fallback if the index is out of bounds of the provided args
                            Console.Write(part);
                        }
                    } else {
                        // Print regular message text
                        Console.Write(part);
                    }
                }
            }
            if (logS != null) {
                // 1. Get the UTF-8 byte representation of the string
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(message);

                // 2. Write the byte array to the stream
                logS.Write(buffer, 0, buffer.Length);
                logS.Flush();
            }
        }
        public static void info(string message, params string[] args) => WriteColored(() => WriteLine(message, args), ConsoleColor.Cyan); 
        public static void Info(string message, int type = 0, params string[] args) => write(ConsoleColor.Cyan, message, () => WriteLine(message, args), type + 0x10);
        public static void InfoT(string message, int type = 0, params string[] args) => write(ConsoleColor.Cyan, message, () => Write(message, args), type + 0x10);
        public static void Log(string message, int type = 0, params string[] args) => write(ConsoleColor.Green, message, () => WriteLine(message, args), type + 0x00);
        public static void LogT(string message, int type = 0, params string[] args) => write(ConsoleColor.Green, message, () => Write(message, args), type + 0x00);
        public static void Warn(string message, int type = 0, params string[] args) => write(ConsoleColor.Yellow, message, () => WriteLine(message, args), type + 0x20);
        public static void WarnT(string message, int type = 0, params string[] args) => write(ConsoleColor.Yellow, message, () => Write(message, args), type + 0x20);
        public static DialogResult Error(string message, int type = 0, params string[] args) => error(message, () => WriteLine(message, args), type);
        public static DialogResult ErrorT(string message, int type = 0, params string[] args) => error(message, () => Write(message, args), type);
        private static DialogResult error(string message, Action Write, int type = 0, params string[] args) =>
                write(ConsoleColor.Red, message, Write, type + 0x30);
        private static DialogResult write(ConsoleColor color, string message, Action Write, int type = 0) {
            if (DEBUG) {
                WriteColored(Write, color);
                switch (type & 0xF) {
                    case 1: Console.Read(); return DialogResult.OK;
                    case 2:
                        char k = Console.ReadKey(true).KeyChar;
                        Console.WriteLine(k + "");
                        if (k == 'y' || k == 'Y') return DialogResult.Yes;
                        else return DialogResult.No;
                }
                return 0;
            }
            switch (type) {
                case 0x20: return MessageBoxShow(message, "Warning");
                case 0x30: return MessageBoxShow(message, "Error");
                case 0x01: return MessageBoxShow(message, "Log", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                case 0x11: return MessageBoxShow(message, "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                case 0x21: return MessageBoxShow(message, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                case 0x31: return MessageBoxShow(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                case 0x02: return MessageBoxShow(message, "Log", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);
                case 0x12: return MessageBoxShow(message, "Info", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                case 0x22: return MessageBoxShow(message, "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                case 0x32: return MessageBoxShow(message, "Error", MessageBoxButtons.YesNo, MessageBoxIcon.Error);
            }
            return 0;
        }
    }
}
