﻿using System.IO;
using System.Net.NetworkInformation;

namespace Protest.Tools {
    internal static class LastSeen {
        public static void Seen(in string ip) {
            try {
                string filename = $"{Data.DIR_LASTSEEN}\\{ip}.txt";
                File.WriteAllText(filename, DateTime.Now.ToString(Data.DATETIME_FORMAT_LONG));
                //File.WriteAllText(filename, DateTime.UtcNow.ToString());
            }
            catch (Exception ex) {
                Logger.Error(ex);
            }
        }

        public static string HasBeenSeen(in string[] para, bool recordOnly = false) {
            string ip = null;
            for (int i = 1; i < para.Length; i++) {
                if (para[i].StartsWith("ip=")) ip = para[i][3..];
            }
            return HasBeenSeen(ip, recordOnly);
        }

        public static string HasBeenSeen(string ip, bool recordOnly = false) {
            if (ip is null) return null;

            if (!recordOnly)
                try {
                    using System.Net.NetworkInformation.Ping p = new System.Net.NetworkInformation.Ping();
                    if (p.Send(ip, 1000).Status == IPStatus.Success) {
                        Seen(ip);
                        return "Just now";
                    }
                }
                catch { }

            string filename = $"{Data.DIR_LASTSEEN}\\{ip}.txt";

            try {
                if (File.Exists(filename))
                    return File.ReadAllText(filename);
            }
            catch { }

            return "Never";
        }

    }
}
