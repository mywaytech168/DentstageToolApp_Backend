using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System;
using System.Globalization;

namespace DentstageToolApp.Api.Infrastructure.System
{
    /// <summary>
    /// 提供本機機碼解析工具，整合環境變數、設定檔與硬體特徵計算最終同步機碼。
    /// </summary>
    public static class LocalMachineKeyResolver
    {
        private const string PrimaryEnvironmentVariable = "SYNC_MACHINE_KEY";
        private const string LegacyEnvironmentVariable = "DENTSTAGE_MACHINE_KEY";
        private const string MachineKeyFileName = ".sync-machine-key";

        /// <summary>
        /// 綜合本機環境推導同步機碼，優先順序為環境變數、外部檔案、設定檔備援與硬體指紋。
        /// </summary>
        /// <param name="configuredValue">設定檔內的預設值，視為最後備援。</param>
        /// <param name="contentRootPath">應用程式根目錄，用於尋找外部機碼檔案。</param>
        /// <returns>解析到的同步機碼，若無法推得則回傳 null。</returns>
        public static string? ResolveMachineKey(string? configuredValue, string contentRootPath)
        {
            // ---------- 環境變數優先 ----------
            // 為方便部署於 Docker 或雲端環境，先嘗試讀取標準環境變數
            var environmentKey = Environment.GetEnvironmentVariable(PrimaryEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(environmentKey))
            {
                return environmentKey.Trim();
            }

            // 兼容舊版命名，避免歷史部署失效
            var legacyEnvironmentKey = Environment.GetEnvironmentVariable(LegacyEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(legacyEnvironmentKey))
            {
                return legacyEnvironmentKey.Trim();
            }

            // ---------- 嘗試讀取外部檔案 ----------
            // 若現場採用 USB Key 或設定檔落地方式，可直接放置於根目錄
            var machineKeyFilePath = Path.Combine(contentRootPath, MachineKeyFileName);
            if (File.Exists(machineKeyFilePath))
            {
                var fileKey = File.ReadAllText(machineKeyFilePath).Trim();
                if (!string.IsNullOrWhiteSpace(fileKey))
                {
                    return fileKey;
                }
            }

            // ---------- 設定檔備援 ----------
            // 若仍讀不到有效機碼，才回頭採用設定檔提供的值
            if (!string.IsNullOrWhiteSpace(configuredValue))
            {
                return configuredValue.Trim();
            }

            // ---------- 硬體指紋 ----------
            // 最後以硬體資訊產生固定指紋，確保至少能推導出穩定機碼
            return GenerateHardwareFingerprint();
        }

        /// <summary>
        /// 透過 CPU ID、主機板序號與主要網卡 MAC 位址產生 SHA256 指紋。
        /// </summary>
        /// <returns>以十六進位大寫呈現的硬體指紋；若無法計算則回傳 null。</returns>
        private static string? GenerateHardwareFingerprint()
        {
            try
            {
                // 依據作業系統取得 CPU ID 與主機板序號；若取不到則回傳 (unknown)
                var cpuId = GetCpuId() ?? "(unknown)";
                var baseboardId = GetBaseboardId() ?? "(unknown)";

                // 過濾虛擬網卡後取得主要乙太網路卡 MAC；若取不到則使用 (no mac)
                var ethernetMac = GetPrimaryEthernetMac() ?? "(no mac)";

                // 參考 Python 實作，將三段資訊組合後進行雜湊
                var raw = string.Join("_", new[] { cpuId, baseboardId, ethernetMac });
                using var sha256 = SHA256.Create();
                var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(raw));
                return Convert.ToHexString(hashBytes);
            }
            catch
            {
                // 若因權限或硬體存取失敗，直接忽略並回傳 null 交由呼叫端決策
                return null;
            }
        }

        /// <summary>
        /// 取得 CPU Processor ID，依照作業系統呼叫對應指令。
        /// </summary>
        private static string? GetCpuId()
        {
            // ---------- Windows 平台 ----------
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var command = "Get-CimInstance Win32_Processor | Select-Object -ExpandProperty ProcessorId";
                return ReadProcessOutput("powershell", $"-Command \"{command}\"");
            }

            // ---------- Linux 平台 ----------
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // 優先使用 dmidecode 提供的 Processor ID
                var dmidecodeResult = ReadProcessOutput("dmidecode", "-t processor");
                if (!string.IsNullOrWhiteSpace(dmidecodeResult))
                {
                    var cpuLine = dmidecodeResult.Split('\n')
                        .FirstOrDefault(line => line.Contains("ID:"));
                    if (!string.IsNullOrWhiteSpace(cpuLine))
                    {
                        return cpuLine.Split(':').Last().Trim();
                    }
                }

                // Fallback：從 /proc/cpuinfo 尋找 Serial 或 ID 欄位
                if (File.Exists("/proc/cpuinfo"))
                {
                    foreach (var line in File.ReadLines("/proc/cpuinfo"))
                    {
                        if (line.Contains("Serial") || line.Contains("ID"))
                        {
                            var parts = line.Split(':');
                            if (parts.Length == 2)
                            {
                                return parts[1].Trim();
                            }
                        }
                    }
                }
            }

            // ---------- macOS 平台 ----------
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var brand = ReadProcessOutput("sysctl", "-n machdep.cpu.brand_string");
                if (!string.IsNullOrWhiteSpace(brand))
                {
                    return brand.Trim();
                }

                return RuntimeInformation.OSDescription;
            }

            // 其他系統以機器名稱作為保底識別資訊
            return Environment.MachineName;
        }

        /// <summary>
        /// 取得主機板序號，依作業系統呼叫相容指令。
        /// </summary>
        private static string? GetBaseboardId()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var command = "Get-WmiObject Win32_BaseBoard | Select-Object -ExpandProperty SerialNumber";
                return ReadProcessOutput("powershell", $"-Command \"{command}\"");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var dmidecodeResult = ReadProcessOutput("dmidecode", "-t baseboard");
                if (!string.IsNullOrWhiteSpace(dmidecodeResult))
                {
                    var serialLine = dmidecodeResult.Split('\n')
                        .FirstOrDefault(line => line.Contains("Serial Number:"));
                    if (!string.IsNullOrWhiteSpace(serialLine))
                    {
                        return serialLine.Split(':').Last().Trim();
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 取得主要的實體乙太網路卡 MAC 位址。
        /// </summary>
        private static string? GetPrimaryEthernetMac()
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
                .Where(nic => nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .OrderByDescending(nic => nic.Speed)
                .ToList();

            foreach (var nic in interfaces)
            {
                // 過濾虛擬或隧道型網卡，模擬 Python 中排除 docker / tun 等介面
                if (IsVirtualInterface(nic))
                {
                    continue;
                }

                var address = nic.GetPhysicalAddress()?.ToString();
                if (!string.IsNullOrWhiteSpace(address) && address != "000000000000")
                {
                    return FormatMacAddress(address);
                }
            }

            // 若所有網卡皆無法使用，退而求其次選擇第一張非迴圈網卡
            var fallback = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Select(nic => nic.GetPhysicalAddress()?.ToString())
                .FirstOrDefault(mac => !string.IsNullOrWhiteSpace(mac) && mac != "000000000000");

            return fallback != null ? FormatMacAddress(fallback) : null;
        }

        /// <summary>
        /// 判斷網卡是否屬於虛擬或隧道型態。
        /// </summary>
        private static bool IsVirtualInterface(NetworkInterface nic)
        {
            var name = nic.Name?.ToLowerInvariant() ?? string.Empty;
            if (name.StartsWith("lo") || name.StartsWith("docker") || name.StartsWith("veth") ||
                name.StartsWith("br-") || name.StartsWith("vmnet") || name.StartsWith("virbr") ||
                name.StartsWith("tun") || name.StartsWith("tap"))
            {
                return true;
            }

            return nic.Description?.ToLowerInvariant().Contains("virtual") == true;
        }

        /// <summary>
        /// 格式化 MAC 位址為以冒號分隔的小寫形式，利於除錯對應。
        /// </summary>
        private static string FormatMacAddress(string mac)
        {
            var cleaned = mac.Replace("-", string.Empty).Replace(":", string.Empty);
            if (cleaned.Length != 12)
            {
                return mac.ToLowerInvariant();
            }

            var segments = Enumerable.Range(0, 6)
                .Select(i => cleaned.Substring(i * 2, 2).ToLowerInvariant());
            return string.Join(":", segments);
        }

        /// <summary>
        /// 以統一方式呼叫外部指令並回傳輸出內容。
        /// </summary>
        private static string? ReadProcessOutput(string fileName, string arguments)
        {
            try
            {
                // ✅ 註冊 Big5 / Shift-JIS / Latin1 等 code page
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                var outputEncoding = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? Encoding.GetEncoding(950) // ✅ Big5
                    : Encoding.UTF8;

                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = arguments,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = outputEncoding,
                        StandardErrorEncoding = outputEncoding
                    }
                };

                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(3000);

                if (!string.IsNullOrWhiteSpace(output))
                {
                    return output.Trim();
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ReadProcessOutput] 發生錯誤：{ex}");
            }

            return null;
        }
    }
}
