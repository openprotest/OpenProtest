﻿
using System;
using System.DirectoryServices;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

public static class ActiveDirectory {
    public static DirectoryEntry GetDirectoryEntry(string domain) {
        if (domain is null) return null;

        DirectoryEntry dir = new DirectoryEntry($"LDAP://{domain}");
        //dir.Username = ".\administrator";
        //dir.Password = "";
        return dir;
    }

    public static bool AuthenticateDomainUser(string username, in string password) {
        string domain = null;

        if (username.Contains("@")) {
            domain = username.Split('@')[1].Trim();
            username = username.Split('@')[0].Trim();
        } else
            try {
                domain = IPGlobalProperties.GetIPGlobalProperties()?.DomainName ?? null;
            } catch { }

        if (domain is null) return false;

        try {
            DirectoryEntry entry = new DirectoryEntry($"LDAP://{domain}", username, password);
            object o = entry.NativeObject;

            using DirectorySearcher searcher = new DirectorySearcher(entry);
            searcher.Filter = $"(SAMAccountName={username})";
            searcher.PropertiesToLoad.Add("cn");

            SearchResult result = searcher.FindOne();
            if (result is null) return false;

        } catch {
            return false;
        }

        return true;
    }

    internal static char[] ActiveDirVerify(string[] para) {
        throw new NotImplementedException();
    }

    public static byte[] GetCurrentNetworkInfo() {
        foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            foreach (UnicastIPAddressInformation ip in nic.GetIPProperties().UnicastAddresses) {
                try {
                    if (IPAddress.IsLoopback(ip.Address)) continue;
                    if (ip.Address.AddressFamily != AddressFamily.InterNetwork) continue;

                    IPAddress subnet = IpTools.GetNetworkAddress(ip.Address, ip.IPv4Mask);
                    IPAddress broadcast = IpTools.GetBroadcastAddress(ip.Address, ip.IPv4Mask);

                    string bits = "";
                    int prefix = 0;
                    for (int i = 0; i < 4; i++) {
                        byte b = ip.IPv4Mask.GetAddressBytes()[i];
                        bits += Convert.ToString(b, 2).PadLeft(8, '0');
                    }
                    for (int i = 0; i < bits.Length; i++) {
                        if (bits[i] == '0') break;
                        prefix++;
                    }

                    string firstAddress = $"{subnet.GetAddressBytes()[0]}.{subnet.GetAddressBytes()[1]}.{subnet.GetAddressBytes()[2]}.{subnet.GetAddressBytes()[3] + 1}";
                    string lastAddress = $"{broadcast.GetAddressBytes()[0]}.{broadcast.GetAddressBytes()[1]}.{broadcast.GetAddressBytes()[2]}.{broadcast.GetAddressBytes()[3] - 1}";
                    string domain = IPGlobalProperties.GetIPGlobalProperties().DomainName;
   
                    string result = "{";
                    result += $"\"firstIp\":\"{firstAddress}\",";
                    result += $"\"lastIp\":\"{lastAddress}\",";
                    result += $"\"domain\":\"{domain}\"";
                    result += "}";

                    return Encoding.UTF8.GetBytes(result);
                } catch { }
            }

        return null;
    }


    public static SearchResult GetUser(string username) {
        string domain = null;
        try {
            domain = IPGlobalProperties.GetIPGlobalProperties()?.DomainName ?? null;
        } catch { }

        DirectoryEntry dir = GetDirectoryEntry(domain);
        using DirectorySearcher searcher = new DirectorySearcher(dir);
        searcher.Filter = "(&(objectClass=user)(objectCategory=person))";
        //searcher.Filter = "(&(objectClass=user)(objectCategory=person)(cn=" + username + "))";

        SearchResultCollection result;
        try {
            result = searcher.FindAll();
            //TODO: try this: searcher.FindOne();
        } catch (Exception ex) {
            Logging.Err(ex);
            return null;
        }

        if (result is null || result.Count == 0) return null;

        username = username.ToLower();

        int index = -1;
        for (int i = 0; i < result.Count; i++) {
            if (result[i].Properties["userPrincipalName"].Count == 0) continue;

            string un = result[i].Properties["userPrincipalName"][0].ToString();

            if (un.Contains("@")) un = un.Substring(0, un.IndexOf("@"));

            if (un.ToLower() == username) {
                index = i;
                break;
            }
        }

        if (index < 0) return null;

        return result[index];
    }

    public static string FileTimeString(string value) {
        long ticks = long.Parse(value);
        if (ticks == 0) return "";
        return DateTime.FromFileTime(ticks).ToString("dddd dd-MMM-yyyy HH:mm:ss");
    }

    public static byte[] UnlockUser(in string[] para) {
        string filename = "", username = "";
        for (int i = 1; i < para.Length; i++) {
            if (para[i].StartsWith("file=")) filename = para[i].Substring(5);
            if (para[i].StartsWith("username=")) username = para[i].Substring(9);
        }

        if (username.Length == 0 && Database.users.ContainsKey(filename)) {
            Database.DbEntry entry = (Database.DbEntry)Database.users[filename];
            if (entry.hash.ContainsKey("USERNAME")) username = ((string[])entry.hash["USERNAME"])[0];
        }

        if (username.Length == 0) return Strings.INF.Array;

        SearchResult sr = GetUser(username);
        if (sr is null) return Encoding.UTF8.GetBytes("not found");

        DirectoryEntry user = new DirectoryEntry(sr.Path);
        user.Properties["LockOutTime"].Value = 0; //unlock account
        user.CommitChanges();
        user.Close();

        return Strings.OK.Array;
    }

    public static byte[] DisableUser(in string[] para) {
        string filename = "", username = "";
        for (int i = 1; i < para.Length; i++) {
            if (para[i].StartsWith("file=")) filename = para[i].Substring(5);
            if (para[i].StartsWith("username=")) username = para[i].Substring(9);
        }

        if (username.Length == 0 && Database.users.ContainsKey(filename)) {
            Database.DbEntry entry = (Database.DbEntry)Database.users[filename];
            if (entry.hash.ContainsKey("USERNAME")) username = ((string[])entry.hash["USERNAME"])[0];
        }

        if (username.Length == 0) return Strings.INF.Array;

        SearchResult sr = GetUser(username);
        if (sr is null) return Strings.INV.Array;

        DirectoryEntry user = new DirectoryEntry(sr.Path);
        int userAccountControl = (int)user.Properties["userAccountControl"].Value;
        userAccountControl |= 0x2;
        user.Properties["userAccountControl"].Value = userAccountControl;

        user.CommitChanges();
        user.Close();

        return Strings.OK.Array;
    }

    public static byte[] EnableUser(in string[] para) {
        string filename = "";
        string username = "";
        for (int i = 1; i < para.Length; i++) {
            if (para[i].StartsWith("file=")) filename = para[i].Substring(5);
            if (para[i].StartsWith("username=")) username = para[i].Substring(9);
        }

        if (username.Length == 0 && Database.users.ContainsKey(filename)) {
            Database.DbEntry entry = (Database.DbEntry)Database.users[filename];
            if (entry.hash.ContainsKey("USERNAME")) username = ((string[])entry.hash["USERNAME"])[0];
        }

        if (username.Length == 0) return Strings.INF.Array;

        SearchResult sr = GetUser(username);
        if (sr is null) return Strings.INV.Array;

        DirectoryEntry user = new DirectoryEntry(sr.Path);
        int userAccountControl = (int)user.Properties["userAccountControl"].Value;
        userAccountControl &= ~0x2;
        user.Properties["userAccountControl"].Value = userAccountControl;

        user.CommitChanges();
        user.Close();

        return Strings.OK.Array;
    }

}
