using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace StepperUpper
{
    internal static class ProcessRunner
    {
        internal static Task<int> RunProcessAsync(string exePath, ProcessPriorityClass priority, params string[] arguments)
        {
            var fullBuilder = new StringBuilder().Append(' ');
            var subBuilder = new StringBuilder();
            foreach (var arg in arguments)
            {
                bool needsQuotes = false;
                foreach (char ch in arg)
                {
                    needsQuotes |= ch == ' ';
                    subBuilder.Append(ch);
                    if (ch == '"')
                    {
                        subBuilder.Append(ch);
                    }
                }

                string sub = subBuilder.MoveToString();

                if (needsQuotes)
                {
                    fullBuilder.Append('"');
                    fullBuilder.Append(sub);
                    fullBuilder.Append('"');
                }
                else
                {
                    fullBuilder.Append(sub);
                }

                fullBuilder.Append(' ');
            }

            --fullBuilder.Length;
            return RunProcessAsync(exePath, priority, fullBuilder.MoveToString());
        }

        private static Task<int> RunProcessAsync(string exePath, ProcessPriorityClass priority, string arguments)
        {
            var process = new Process();
            try
            {
                process.StartInfo = new ProcessStartInfo(exePath, arguments)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                };

                process.EnableRaisingEvents = true;

                var tcs = new TaskCompletionSource<int>();
                process.Exited += (_, __) => tcs.SetResult(process.ExitCode);

                if (!process.Start())
                {
                    return Task.FromException<int>(new InvalidOperationException("Unable to start process."));
                }

                try
                {
                    process.PriorityClass = priority;
                }
                catch
                {
                }

                return tcs.Task.Finally(process.Dispose);
            }
            catch (Exception ex)
            {
                process.Dispose();
                return Task.FromException<int>(ex);
            }
        }
    }
}
