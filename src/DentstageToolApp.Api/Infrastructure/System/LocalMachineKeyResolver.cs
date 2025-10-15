using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

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
        /// 以機器名稱、作業系統描述與主要網路卡 MAC 位址產生 SHA256 指紋。
        /// </summary>
        /// <returns>以十六進位大寫呈現的硬體指紋；若無法計算則回傳 null。</returns>
        private static string? GenerateHardwareFingerprint()
        {
            try
            {
                var identifiers = new List<string>();

                // 收集機器名稱與作業系統描述，作為最基本的識別資訊
                identifiers.Add(Environment.MachineName);
                identifiers.Add(RuntimeInformation.OSDescription);

                // 選擇第一張可用網卡的 MAC 位址，提升跨平台穩定性
                var macAddress = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
                    .Where(nic => nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .Select(nic => nic.GetPhysicalAddress()?.ToString())
                    .FirstOrDefault(address => !string.IsNullOrWhiteSpace(address));

                if (!string.IsNullOrWhiteSpace(macAddress))
                {
                    identifiers.Add(macAddress);
                }

                var rawFingerprint = string.Join("|", identifiers.Where(item => !string.IsNullOrWhiteSpace(item)));
                if (string.IsNullOrWhiteSpace(rawFingerprint))
                {
                    return null;
                }

                // 採用 SHA256 產生固定長度指紋，避免直接暴露硬體資訊
                using var sha256 = SHA256.Create();
                var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawFingerprint));
                return Convert.ToHexString(hashBytes);
            }
            catch
            {
                // 若因權限或硬體存取失敗，直接忽略並回傳 null 交由呼叫端決策
                return null;
            }
        }
    }
}
