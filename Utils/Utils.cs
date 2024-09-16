using EnvDTE;
using EnvDTE80;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpUnitTestGeneratorExt.Utils
{
    public static class Utils
    {
        public static void ExecuteDotnetTestCommand(string csprojFilePath, string workingPath)
        {
            // 创建一个新的进程信息
            ProcessStartInfo processStartInfo = new ProcessStartInfo
            {
                FileName = "dotnet.exe", // 指定要启动的程序（命令提示符）
                WorkingDirectory = workingPath,
                Arguments = $"test {csprojFilePath}", // 传递给cmd的参数，/c表示执行完命令后关闭命令窗口
                UseShellExecute = false, // 是否使用操作系统shell启动
                RedirectStandardOutput = true, // 重定向标准输出，这样就可以从Process对象中读取输出
                RedirectStandardError = true,
                CreateNoWindow = false // 不创建新窗口
            };

            // 创建并启动进程
            using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(processStartInfo))
            {
                // 读取输出信息
                string result = process.StandardOutput.ReadToEnd();

                // 等待进程执行完毕
                process.WaitForExit();

                // 处理结果，比如输出到Visual Studio的输出窗口
                Debug.WriteLine(result);

                result = process.StandardOutput.ReadToEnd();
                Debug.WriteLine(result);
            }
        }

        public static void ExecuteCommandInBuiltInWindow(DTE2 dte, string command, string workingPath)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            dte.ExecuteCommand("View.PowerShellInteractiveConsole");
            dte.ExecuteCommand("cd D:\\");  

            Window window = dte.Windows.Item(Constants.vsWindowKindCommandWindow);
            window.Activate();
            window.Visible = true;
            CommandWindow commandWindow = (CommandWindow)window.Object;
            commandWindow.SendInput($"cd /d {workingPath}", true);
            commandWindow.SendInput("YourCommand", true);
        }

        public static string GetDebuggerHelperFileContent(string fileName, string CompanyInFileHeaderCopyright)
        {
            string fileHeaderCopyRightPart = $@"// <copyright file=""{fileName}"" company=""{CompanyInFileHeaderCopyright}"">
//     Copyright (c) {CompanyInFileHeaderCopyright} Corporation.  All rights reserved.
// </copyright>
";
            return string.Concat(fileHeaderCopyRightPart, ExtConstant.DebuggerHelperFileContent);
        }
    }
}
