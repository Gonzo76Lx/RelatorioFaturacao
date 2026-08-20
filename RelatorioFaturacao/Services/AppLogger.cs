using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RelatorioFaturacao.Services
{
    public static class AppLogger
    {
        private static readonly object _lockObj = new();
        private static readonly ConcurrentQueue<string> _memoryLogBuffer = new();
        private const int MaxMemoryLogLines = 1000;
        private static string? _logDirectory;
        private static string? _currentLogFilePath;

        public static event Action<string>? OnLogAdded;

        static AppLogger()
        {
            try
            {
                // Set up log directory in AppData and in Desktop/relatorifaturacao if accessible
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string localAppDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RelatorioFaturacao", "logs");

                // Prefer logs subfolder in base directory or LocalApplicationData
                string targetDir = Path.Combine(baseDir, "logs");
                try
                {
                    if (!Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }
                    _logDirectory = targetDir;
                }
                catch
                {
                    if (!Directory.Exists(localAppDir))
                    {
                        Directory.CreateDirectory(localAppDir);
                    }
                    _logDirectory = localAppDir;
                }

                string fileName = $"relatorio_faturacao_{DateTime.Now:yyyy-MM-dd}.log";
                _currentLogFilePath = Path.Combine(_logDirectory, fileName);

                LogInfo("=== Sessão de Aplicação Iniciada ===");
                LogInfo($"Diretório de Logs: {_logDirectory}");
                LogInfo($"SO: {Environment.OSVersion} | .NET: {Environment.Version} | Processo: {(Environment.Is64BitProcess ? "64-bit" : "32-bit")}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppLogger Init Error] {ex.Message}");
            }
        }

        public static string LogDirectory => _logDirectory ?? AppDomain.CurrentDomain.BaseDirectory;
        public static string CurrentLogFilePath => _currentLogFilePath ?? Path.Combine(LogDirectory, $"relatorio_faturacao_{DateTime.Now:yyyy-MM-dd}.log");

        public static void LogInfo(string message) => Write("INFO", message);
        public static void LogWarning(string message) => Write("WARN", message);
        public static void LogError(string message, Exception? ex = null)
        {
            var sb = new StringBuilder();
            sb.Append(message);
            if (ex != null)
            {
                sb.AppendLine();
                sb.AppendLine($"   [Tipo de Exceção]: {ex.GetType().FullName}");
                sb.AppendLine($"   [Mensagem]: {ex.Message}");
                if (!string.IsNullOrWhiteSpace(ex.StackTrace))
                {
                    sb.AppendLine($"   [Stack Trace]:\n{ex.StackTrace}");
                }
                if (ex.InnerException != null)
                {
                    sb.AppendLine($"   [Inner Exception]: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}");
                    if (!string.IsNullOrWhiteSpace(ex.InnerException.StackTrace))
                    {
                        sb.AppendLine($"   [Inner Stack Trace]:\n{ex.InnerException.StackTrace}");
                    }
                }
            }
            Write("ERROR", sb.ToString());
        }

        public static void LogDebug(string message) => Write("DEBUG", message);

        private static void Write(string level, string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var threadId = Thread.CurrentThread.ManagedThreadId;
            var formattedLine = $"[{timestamp}] [{level.PadRight(5)}] [Thread {threadId:D2}] {message}";

            Debug.WriteLine(formattedLine);

            _memoryLogBuffer.Enqueue(formattedLine);
            while (_memoryLogBuffer.Count > MaxMemoryLogLines)
            {
                _memoryLogBuffer.TryDequeue(out _);
            }

            OnLogAdded?.Invoke(formattedLine);

            try
            {
                lock (_lockObj)
                {
                    if (!string.IsNullOrEmpty(_currentLogFilePath))
                    {
                        var dir = Path.GetDirectoryName(_currentLogFilePath);
                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }
                        File.AppendAllText(_currentLogFilePath, formattedLine + Environment.NewLine, Encoding.UTF8);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppLogger Write Error] {ex.Message}");
            }
        }

        public static string GetAllLogsText()
        {
            try
            {
                lock (_lockObj)
                {
                    if (File.Exists(CurrentLogFilePath))
                    {
                        return File.ReadAllText(CurrentLogFilePath, Encoding.UTF8);
                    }
                }
            }
            catch (Exception ex)
            {
                return $"Erro ao ler ficheiro de log: {ex.Message}\n\nLogs em memória:\n" + string.Join(Environment.NewLine, _memoryLogBuffer);
            }

            return string.Join(Environment.NewLine, _memoryLogBuffer);
        }

        public static IReadOnlyList<string> GetRecentLogLines()
        {
            return _memoryLogBuffer.ToArray();
        }

        public static void OpenLogFile()
        {
            try
            {
                if (File.Exists(CurrentLogFilePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = CurrentLogFilePath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    OpenLogsFolder();
                }
            }
            catch (Exception ex)
            {
                LogError("Falha ao abrir ficheiro de log", ex);
            }
        }

        public static void OpenLogsFolder()
        {
            try
            {
                if (Directory.Exists(LogDirectory))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = LogDirectory,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                LogError("Falha ao abrir pasta de logs", ex);
            }
        }

        public static void ClearLogs()
        {
            try
            {
                lock (_lockObj)
                {
                    if (File.Exists(CurrentLogFilePath))
                    {
                        File.WriteAllText(CurrentLogFilePath, string.Empty);
                    }
                    while (_memoryLogBuffer.TryDequeue(out _)) { }
                }
                LogInfo("=== Logs limpos pelo utilizador ===");
            }
            catch (Exception ex)
            {
                LogError("Falha ao limpar logs", ex);
            }
        }
    }
}
