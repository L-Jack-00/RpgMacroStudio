using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace RpgMacroStudio.Launcher;

static class Program
{
    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int dwProcessId);

    private const int ATTACH_PARENT_PROCESS = -1;

    [STAThread]
    static int Main(string[] args)
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string dataExe = Path.Combine(baseDir, "data", "RpgMacroStudio.exe");

        if (!File.Exists(dataExe))
        {
            MessageBox.Show(
                "실행에 필요한 핵심 데이터 파일(data\\RpgMacroStudio.exe)을 찾을 수 없습니다.\n프로그램이 올바르게 설치되었는지 확인해주세요.\n\nCould not find 'data\\RpgMacroStudio.exe'.",
                "RPG 매크로 스튜디오 (RPG Macro Studio)",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }

        bool isCli = args != null && args.Length > 0;

        var psi = new ProcessStartInfo
        {
            FileName = dataExe,
            WorkingDirectory = baseDir,
            UseShellExecute = false
        };

        if (isCli)
        {
            AttachConsole(ATTACH_PARENT_PROCESS);
            psi.Arguments = BuildCommandLine(args!);
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;

            try
            {
                using var process = Process.Start(psi);
                if (process == null) return 1;

                process.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null) Console.Out.WriteLine(e.Data);
                };
                process.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null) Console.Error.WriteLine(e.Data);
                };

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                process.WaitForExit();
                return process.ExitCode;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("실행 오류: " + ex.Message);
                return 1;
            }
        }
        else
        {
            try
            {
                Process.Start(psi);
                return 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "프로그램 실행 중 오류가 발생했습니다:\n" + ex.Message,
                    "RPG 매크로 스튜디오",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return 1;
            }
        }
    }

    private static string BuildCommandLine(string[] args)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < args.Length; i++)
        {
            if (i > 0) sb.Append(' ');
            string a = args[i];
            if (a.Contains(' ') || a.Contains('"'))
            {
                sb.Append('"').Append(a.Replace("\"", "\\\"")).Append('"');
            }
            else
            {
                sb.Append(a);
            }
        }
        return sb.ToString();
    }
}
