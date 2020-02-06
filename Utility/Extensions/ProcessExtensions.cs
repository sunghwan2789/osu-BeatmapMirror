using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Utility.Extensions
{
    public static class ProcessExtensions
    {
        public static Task WaitForExitAsync(this Process process, CancellationToken cancellationToken = default)
        {
            var taskCompletionSource = new TaskCompletionSource<object>();

            process.EnableRaisingEvents = true;
            process.Exited += handler;

            if (cancellationToken != default)
            {
                cancellationToken.Register(() =>
                {
                    process.Exited -= handler;
                    taskCompletionSource.TrySetCanceled();
                });
            }

            return taskCompletionSource.Task;

            void handler(object sender, EventArgs e)
            {
                process.Exited -= handler;
                taskCompletionSource.TrySetResult(null);
            }
        }

        public static Task OutputReadToEndAsync(this Process process, StringBuilder stringBuilder, CancellationToken cancellationToken = default)
        {
            var taskCompletionSource = new TaskCompletionSource<string>();

            process.OutputDataReceived += handler;
            process.BeginOutputReadLine();

            if (cancellationToken != default)
            {
                cancellationToken.Register(() =>
                {
                    process.OutputDataReceived -= handler;
                    taskCompletionSource.TrySetCanceled();
                });
            }

            return taskCompletionSource.Task;

            void handler(object sender, DataReceivedEventArgs e)
            {
                if (e.Data == null)
                {
                    process.OutputDataReceived -= handler;
                    taskCompletionSource.TrySetResult(null);
                }
                else
                {
                    stringBuilder.AppendLine(e.Data);
                }
            }
        }
    }
}