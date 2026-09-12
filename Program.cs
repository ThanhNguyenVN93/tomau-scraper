using System;
using System.Diagnostics;
using Microsoft.Win32;
using System.Windows.Forms;

namespace ToMauScraper
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (!IsWindows7Sp1OrNewer())
            {
                MessageBox.Show(
                    "Ứng dụng yêu cầu tối thiểu Windows 7 SP1.\nHệ điều hành hiện tại không được hỗ trợ.",
                    "Không tương thích", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!HasDotNet472OrNewer())
            {
                var result = MessageBox.Show(
                    "Máy chưa cài .NET Framework 4.7.2 (hoặc mới hơn) — ứng dụng cần bản này để chạy đúng.\n\n" +
                    "Bấm \"Yes\" để mở trang tải xuống chính thức từ Microsoft.",
                    "Thiếu .NET Framework 4.7.2", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(
                        "https://dotnet.microsoft.com/download/dotnet-framework/net472")
                    { UseShellExecute = true });
                }
                return;
            }

            Application.Run(new Form1());
        }

        // ─── Windows 7 SP1 trở lên ──────────────────────────────────────────────
        private static bool IsWindows7Sp1OrNewer()
        {
            var os = Environment.OSVersion.Version;
            if (os.Major > 6) return true;                 // Windows 10/11 (10.0)
            if (os.Major == 6 && os.Minor >= 2) return true; // Windows 8 / 8.1
            if (os.Major == 6 && os.Minor == 1)              // Windows 7
                return Environment.OSVersion.ServicePack.Contains("Service Pack 1");
            return false; // Vista trở xuống
        }

        // ─── .NET Framework 4.7.2 (Release >= 461808) ──────────────────────────
        private static bool HasDotNet472OrNewer()
        {
            const int net472Release = 461808;
            try
            {
                using var ndpKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32)
                    .OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\");
                if (ndpKey?.GetValue("Release") is int release)
                    return release >= net472Release;
            }
            catch { }
            return false;
        }
    }
}
