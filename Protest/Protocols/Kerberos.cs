﻿using System.Collections.Generic;
using System.DirectoryServices;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Runtime.Versioning;

namespace Protest.Protocols;

internal static class Kerberos {

    [SupportedOSPlatform("windows")]
    public static string[] GetAllWorkstations(string domain) {
        if (string.IsNullOrEmpty(domain)) return null;

        SearchResultCollection result = null;

        try {
            DirectoryEntry directoryEntry = new DirectoryEntry($"LDAP://{domain}");
            using DirectorySearcher searcher = new DirectorySearcher(directoryEntry);
            searcher.Filter = "(objectClass=computer)";
            //result = searcher.FindAll().Cast<SearchResult>().ToArray();
            result = searcher.FindAll();

        }
        catch (Exception ex) {
            Logger.Error(ex);
            return null;
        }

        if (result is null || result.Count == 0) return null;

        string[] array = new string[result.Count];
        for (int i = 0; i < result.Count; i++) {
            array[i] = result[i].Properties.Contains("name") ? result[i].Properties["name"][0].ToString() : string.Empty;
        }

        return array;
    }

    [SupportedOSPlatform("windows")]
    public static string[] GetAllUsers(string domain) {
        SearchResultCollection result = null;
        try {
            DirectoryEntry dir = new DirectoryEntry($"LDAP://{domain}");
            using DirectorySearcher searcher = new DirectorySearcher(dir);
            searcher.Filter = "(&(objectClass=user)(objectCategory=person))";
            result = searcher.FindAll();
        }
        catch (Exception ex) {
            Logger.Error(ex);
            return null;
        }

        if (result is null || result.Count == 0) return null;

        List<string> list = new List<string>();
        foreach (SearchResult o in result)
            if (o.Properties.Contains("userPrincipalName") && o.Properties["userPrincipalName"].Count > 0)
                list.Add(o.Properties["userPrincipalName"][0].ToString());

        return list.ToArray();
    }

    [SupportedOSPlatform("windows")]
    public static bool TryDirectoryAuthenticate(string username, string password) {
        string domain = null;

        if (username.Contains('@')) {
            domain = username.Split('@')[1].Trim();
            username = username.Split('@')[0].Trim();
        }
        else {
            try {
                domain = IPGlobalProperties.GetIPGlobalProperties()?.DomainName ?? null;
            }
            catch { }
        }

        if (domain is null) return false;

        try {
            DirectoryEntry entry = new DirectoryEntry($"LDAP://{domain}", username, password);
            object o = entry.NativeObject;

            using DirectorySearcher searcher = new DirectorySearcher(entry);
            searcher.Filter = $"(SAMAccountName={username})";
            searcher.PropertiesToLoad.Add("cn");

            SearchResult result = searcher.FindOne();
            if (result is null) return false;

        }
        catch {
            return false;
        }

        return true;
    }

    public static byte[] NetworkInfo() {
        foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            foreach (UnicastIPAddressInformation ip in nic.GetIPProperties().UnicastAddresses) {
                try {
                    if (IPAddress.IsLoopback(ip.Address)) continue;
                    if (ip.Address.AddressFamily != AddressFamily.InterNetwork) continue;

                    IPAddress subnet = IpTools.GetNetworkAddress(ip.Address, ip.IPv4Mask);
                    IPAddress broadcast = IpTools.GetBroadcastAddress(ip.Address, ip.IPv4Mask);

                    string bits = string.Empty;
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
                }
                catch { }
            }

        return null;
    }

    [SupportedOSPlatform("windows")]
    public static SearchResult GetWorkstation(string name) {
        if (string.IsNullOrEmpty(name)) return null;

        string domain = null;
        try {
            domain = IPGlobalProperties.GetIPGlobalProperties()?.DomainName ?? null;
        }
        catch { }

        if (name.Contains('.')) name = name.Split('.')[0];

        DirectoryEntry directoryEntry = new DirectoryEntry($"LDAP://{domain}");
        using DirectorySearcher searcher = new DirectorySearcher(directoryEntry);
        searcher.Filter = $"(&(objectClass=computer)(cn={name}))";

        SearchResult result = null;
        try {
            result = searcher.FindOne();
        }
        catch {
            return null;
        }

        if (result is null) return null;
        return result;
    }

    [SupportedOSPlatform("windows")]
    public static SearchResult GetUser(string username) {
        if (string.IsNullOrEmpty(username)) return null;

        string domain = null;
        try {
            domain = IPGlobalProperties.GetIPGlobalProperties()?.DomainName ?? null;
        }
        catch { }

        using DirectoryEntry directoryEntry = new DirectoryEntry($"LDAP://{domain}");
        SearchResult result = null;

        try {
            using DirectorySearcher searcher = new DirectorySearcher(directoryEntry);
            searcher.Filter = $"(&(objectClass=user)(objectCategory=person)(userPrincipalName={username}))";
            result = searcher.FindOne();
        }
        catch { }

        if (result is null)
            try {
                if (username.IndexOf("@") > -1) username = username.Split('@')[0];
                using DirectorySearcher searcher = new DirectorySearcher(directoryEntry);
                //searcher.Filter = "(&(objectClass=user)(objectCategory=person))";
                searcher.Filter = $"(&(objectClass=user)(objectCategory=person)(cn={username}))";
                result = searcher.FindOne();
            }
            catch { }

        if (result is null) {
            username = username.ToLower();

            using DirectorySearcher searcher = new DirectorySearcher(directoryEntry);
            searcher.Filter = "(&(objectClass=user)(objectCategory=person))";

            try {
                SearchResultCollection allUsers = searcher.FindAll();
                if (allUsers is null || allUsers.Count == 0) return null;

                for (int i = 0; i < allUsers.Count; i++) {
                    if (allUsers[i].Properties["userPrincipalName"].Count == 0) continue;
                    string un = allUsers[i].Properties["userPrincipalName"][0].ToString();

                    if (un.Contains('@')) un = un[..un.IndexOf("@")];
                    if (un.ToLower() == username) return allUsers[i];
                }
            }
            catch { }
        }

        if (result is null) return null;
        return result;
    }

    private delegate string FormatMethodPtr(string value);
    [SupportedOSPlatform("windows")]
    private static void ContentBuilderAddValue(SearchResult sr, string property, string label, Dictionary<string, string> date, FormatMethodPtr format = null) {
        for (int i = 0; i < sr.Properties[property].Count; i++) {
            string value = sr.Properties[property][i].ToString();
            if (value.Length > 0) {
                if (format != null) value = format.Invoke(value);
                if (string.IsNullOrEmpty(value)) continue;
                date.Add(label, value);
                break;
            }
        }
    }

    [SupportedOSPlatform("windows")]
    public static Dictionary<string, string> AdFetch(string username) {
        SearchResult sr = GetUser(username);
        if (sr is null) return null;
        return AdFetch(sr);
    }
    [SupportedOSPlatform("windows")]
    public static Dictionary<string, string> AdFetch(SearchResult result) {
        Dictionary<string, string> data = new Dictionary<string, string> {
            { "type", "Domain user" }
        };

        ContentBuilderAddValue(result, "title", "title", data, null);
        if (!data.ContainsKey("title")) ContentBuilderAddValue(result, "personalTitle", "title", data, null);

        ContentBuilderAddValue(result, "givenname", "first name", data, null);
        ContentBuilderAddValue(result, "middlename", "middle name", data, null);
        ContentBuilderAddValue(result, "sn", "last name", data, null);

        ContentBuilderAddValue(result, "userprincipalname", "username", data, GetUsername);
        ContentBuilderAddValue(result, "distinguishedname", "distinguished name", data, null);
        ContentBuilderAddValue(result, "displayname", "display name", data, null);

        ContentBuilderAddValue(result, "employeeid", "employee id", data, null);
        ContentBuilderAddValue(result, "company", "company", data, null);
        ContentBuilderAddValue(result, "department", "department", data, null);
        ContentBuilderAddValue(result, "division", "division", data, null);
        ContentBuilderAddValue(result, "comment", "comment", data, null);

        ContentBuilderAddValue(result, "mail", "e-mail", data, null);
        ContentBuilderAddValue(result, "othermailbox", "secondary e-mail", data, null);

        StringBuilder telephoneBuilder = new StringBuilder();
        string telephoneNumber = result.Properties["telephonenumber"][0].ToString();
        if (telephoneNumber?.Length > 0) telephoneBuilder.Append(telephoneNumber);

        ResultPropertyValueCollection otherTelephone = result.Properties["othertelephone"];
        for (int i = 0; i< otherTelephone.Count; i++) {
            telephoneBuilder.Append(telephoneBuilder.Length > 0 ? $"; {otherTelephone[i]}" : otherTelephone[i].ToString());
        }
        data.Add("telephone number", telephoneBuilder.ToString());

        StringBuilder mobileBuilder = new StringBuilder();
        string mobileNumber = result.Properties["mobile"][0].ToString();
        if (mobileNumber?.Length > 0)
            mobileBuilder.Append(mobileNumber);

        ResultPropertyValueCollection otherMobile = result.Properties["othermobile"];
        for (int i = 0; i < otherMobile.Count; i++) {
            mobileBuilder.Append(mobileBuilder.Length > 0 ? $"; {otherMobile[i]}" : otherMobile[i].ToString());
        }
        data.Add("mobile number", mobileBuilder.ToString());

        ContentBuilderAddValue(result, "facsimiletelephonenumber", "fax", data, null);

        data.Add("guid", new Guid((byte[])result.Properties["objectGuid"][0]).ToString());

        return data;
    }

    private static string GetUsername(string value) {
        int index = value.IndexOf('@');
        return index > -1 ? value[..index] : value;
    }

    public static string FileTimeString(string value) {
        long ticks = long.Parse(value);
        if (ticks == 0) return string.Empty;
        return DateTime.FromFileTime(ticks).ToString("dddd dd-MMM-yyyy HH:mm:ss");
    }

    [SupportedOSPlatform("windows")]
    public static byte[] UnlockUser(Dictionary<string, string> parameters) {
        if (parameters is null) {
            return Data.CODE_INVALID_ARGUMENT.Array;
        }

        parameters.TryGetValue("file", out string file);

        string username = String.Empty;

        if (DatabaseInstances.users.dictionary.TryGetValue(file, out Database.Entry entry)) {
            if (entry.attributes.TryGetValue("username", out Database.Attribute macValue)) {
                username = macValue.value;
            }
        }
        else {
            return Data.CODE_FILE_NOT_FOUND.Array;
        }

        if (username.Length == 0) return Data.CODE_NOT_ENOUGH_INFO.Array;

        SearchResult sr = GetUser(username);
        if (sr is null) return Data.CODE_NOT_FOUND.Array;

        DirectoryEntry user = new DirectoryEntry(sr.Path);
        user.Properties["LockOutTime"].Value = 0; //unlock account
        user.CommitChanges();
        user.Close();

        return Data.CODE_OK.Array;
    }

    [SupportedOSPlatform("windows")]
    public static byte[] EnableUser(Dictionary<string, string> parameters) {
        if (parameters is null) {
            return Data.CODE_INVALID_ARGUMENT.Array;
        }

        parameters.TryGetValue("file", out string file);

        string username = String.Empty;

        if (DatabaseInstances.users.dictionary.TryGetValue(file, out Database.Entry entry)) {
            if (entry.attributes.TryGetValue("username", out Database.Attribute macValue)) {
                username = macValue.value;
            }
        }
        else {
            return Data.CODE_FILE_NOT_FOUND.Array;
        }

        if (username.Length == 0) return Data.CODE_NOT_ENOUGH_INFO.Array;

        SearchResult sr = GetUser(username);
        if (sr is null) return Data.CODE_NOT_FOUND.Array;

        DirectoryEntry user = new DirectoryEntry(sr.Path);
        int userAccountControl = (int)user.Properties["userAccountControl"].Value;
        userAccountControl &= ~0x2;
        user.Properties["userAccountControl"].Value = userAccountControl;

        user.CommitChanges();
        user.Close();

        return Data.CODE_OK.Array;
    }

    [SupportedOSPlatform("windows")]
    public static byte[] DisableUser(Dictionary<string, string> parameters) {
        if (parameters is null) {
            return Data.CODE_INVALID_ARGUMENT.Array;
        }

        parameters.TryGetValue("file", out string file);

        string username = String.Empty;

        if (DatabaseInstances.users.dictionary.TryGetValue(file, out Database.Entry entry)) {
            if (entry.attributes.TryGetValue("username", out Database.Attribute macValue)) {
                username = macValue.value;
            }
        }
        else {
            return Data.CODE_FILE_NOT_FOUND.Array;
        }

        if (username.Length == 0) return Data.CODE_NOT_ENOUGH_INFO.Array;

        SearchResult sr = GetUser(username);
        if (sr is null) return Data.CODE_NOT_FOUND.Array;

        DirectoryEntry user = new DirectoryEntry(sr.Path);
        int userAccountControl = (int)user.Properties["userAccountControl"].Value;
        userAccountControl |= 0x2;
        user.Properties["userAccountControl"].Value = userAccountControl;

        user.CommitChanges();
        user.Close();

        return Data.CODE_OK.Array;
    }
}