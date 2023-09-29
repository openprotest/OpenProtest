﻿#if !DEBUG && NET7_0_OR_GREATER
//#define DEFLATE
#define BROTLI
#endif

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net;
using System.Security.AccessControl;

namespace Protest.Http;

public sealed class Listener {
    private readonly HttpListener listener;
    private readonly Cache cache;

    public Listener(string ip, ushort port, string path) {
        if (!HttpListener.IsSupported) throw new NotSupportedException();
        cache = new Cache(path);
        listener = new HttpListener();
        Bind(new string[] { $"http://{ip}:{port}/" });
    }

    public Listener(string[] uriPrefixes, string path) {
        if (!HttpListener.IsSupported) throw new NotSupportedException();
        cache = new Cache(path);
        listener = new HttpListener();
        Bind(uriPrefixes);
    }

    ~Listener() {
        Stop();
    }

    private void Bind(string[] uriPrefixes) {
        listener.IgnoreWriteExceptions = true;

        for (int i = 0; i < uriPrefixes.Length; i++)
            listener.Prefixes.Add(uriPrefixes[i]);

        try {
            listener.Start();
        }
        catch (HttpListenerException ex) {
            Logger.Error(ex);
            throw;
        }
    }

    public void Start() {
        while (listener.IsListening) {
            IAsyncResult result = listener.BeginGetContext(ListenerCallback, listener);
            result.AsyncWaitHandle.WaitOne();
        }
        Console.WriteLine("Listener stopped");
    }

    public void Stop() {
        if (listener is not null && listener.IsListening) listener.Stop();
        listener.Abort();
    }

    private void ListenerCallback(IAsyncResult result) {
        HttpListenerContext ctx = listener.EndGetContext(result);

        //Cross Site Request Forgery protection
        if (ctx.Request.UrlReferrer != null) {
            if (!string.Equals(ctx.Request.UrlReferrer.Host, ctx.Request.UserHostName.Split(':')[0], StringComparison.Ordinal)) {
                ctx.Response.StatusCode = 418; //I'm a teapot
                ctx.Response.Close();
                return;
            }
            if (Uri.IsWellFormedUriString(ctx.Request.UrlReferrer.Host, UriKind.Absolute)) {
                ctx.Response.StatusCode = 418; //I'm a teapot
                ctx.Response.Close();
                return;
            }
            UriHostNameType type = Uri.CheckHostName(ctx.Request.UrlReferrer.Host);
            if (type != UriHostNameType.Dns && type != UriHostNameType.IPv4 && type != UriHostNameType.IPv6) {
                ctx.Response.StatusCode = 418; //I'm a teapot
                ctx.Response.Close();
                return;
            }
        }

        string path = ctx.Request.Url.PathAndQuery;

        if (string.Equals(path, "/auth", StringComparison.Ordinal)) {
            if (!string.Equals(ctx.Request.HttpMethod, "POST", StringComparison.Ordinal)) {
                ctx.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                ctx.Response.Close();
                return;
            }

            ctx.Response.StatusCode = Auth.AttemptAuthentication(ctx, out _) ?
                (int)HttpStatusCode.Accepted :
                (int)HttpStatusCode.Unauthorized;

            ctx.Response.Close();
            return;
        }


        if (CacheHandler(ctx, path)) return;

        if (!Auth.IsAuthenticated(ctx)) {
            ctx.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            ctx.Response.Close();
            return;
        }

        if (!Auth.IsAuthorized(ctx, ctx.Request.Url.AbsolutePath)) {
            ctx.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            ctx.Response.Close();
            return;
        }

        if (WebSocketHandler(ctx)) return;

        Dictionary<string, string> parameters = null;
        string query = ctx.Request.Url.Query;
        if (query.Length > 0) {
            parameters = ParseQuery(query);
        }

        if (DynamicHandler(ctx, parameters)) return;

        ctx.Response.StatusCode = (int)HttpStatusCode.NotFound;
        ctx.Response.Close();
    }

    public static Dictionary<string, string> ParseQuery(string queryString) {
        if (string.IsNullOrEmpty(queryString)) return null;

        Dictionary<string, string> parameters = new Dictionary<string, string>();

        var span = queryString.AsSpan();
        if (span.StartsWith("?")) span = span[1..];

        while (!span.IsEmpty) {
            int equalsIndex = span.IndexOf('=');
            if (equalsIndex < 0) continue;

            ReadOnlySpan<char> key = span[..equalsIndex];
            span = span[(equalsIndex + 1)..];

            int ampersandIndex = span.IndexOf('&');
            ReadOnlySpan<char> value;

            if (ampersandIndex >= 0) {
                value = span[..ampersandIndex];
                span = span[(ampersandIndex + 1)..];
            }
            else {
                value = span;
                span = span[span.Length..];
            }

            parameters[Uri.UnescapeDataString(key.ToString())] = Uri.UnescapeDataString(value.ToString());
        }

        return parameters;
    }

    private bool CacheHandler(HttpListenerContext ctx, string path) {
        if (!cache.cache.ContainsKey(path)) return false;

        Cache.Entry entry;
        if (string.Equals(path, "/", StringComparison.Ordinal)) {
            if (!Auth.IsAuthenticated(ctx)) {
                entry = cache.cache.TryGetValue("/login", out Cache.Entry value) ? value : default;
            }
            else {
                entry = cache.cache["/"];
            }
        }
        else {
            entry = cache.cache[path];
        }

        string acceptEncoding = ctx.Request.Headers.Get("Accept-Encoding")?.ToLower() ?? String.Empty;
        bool acceptGZip = acceptEncoding.Contains("gzip");

        byte[] buffer;
#if BROTLI
        bool acceptBrotli = acceptEncoding.Contains("br");
        if (acceptBrotli && entry.brotli is not null) { //brotli
            buffer = entry.brotli;
            ctx.Response.AddHeader("Content-Encoding", "br");
        }
        else
#endif
#if DEFLATE
        bool acceptDeflate = acceptEncoding.Contains("deflate");
        if (acceptDeflate && entry.deflate is not null) { //deflate
            buffer = entry.deflate;
            ctx.Response.AddHeader("Content-Encoding", "deflate");
        } else
#endif
        if (acceptGZip && entry.gzip is not null) { //gzip
            buffer = entry.gzip;
            ctx.Response.AddHeader("Content-Encoding", "gzip");
        }
        else { //raw
            buffer = entry.bytes;
        }

        ctx.Response.StatusCode = (int)HttpStatusCode.OK;
        ctx.Response.ContentType = entry.contentType;
        ctx.Response.AddHeader("Length", buffer?.Length.ToString() ?? "0");

        for (int i = 0; i < entry.headers.Length; i++) {
            ctx.Response.AddHeader(entry.headers[i].Key, entry.headers[i].Value);
        }

        try {
            if (buffer is not null) ctx.Response.OutputStream.Write(buffer, 0, buffer.Length);
            ctx.Response.OutputStream.Flush();
#if DEBUG
        }
        catch (HttpListenerException ex) {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine(ex.StackTrace);
        }
#else
        }
        catch (HttpListenerException) { /*do nothing*/ }
#endif

        ctx.Response.Close();
        return true;
    }

    private static bool DynamicHandler(HttpListenerContext ctx, Dictionary<string, string> parameters) {
        string sessionId = ctx.Request.Cookies["sessionid"]?.Value ?? null;
        string username = IPAddress.IsLoopback(ctx.Request.RemoteEndPoint.Address) ? "loopback" : Auth.GetUsername(sessionId);

        ctx.Response.AddHeader("Cache-Control", "no-cache");

        byte[] buffer;

        switch (ctx.Request.Url.AbsolutePath) {
        case "/logout" : buffer = Auth.RevokeAccess(sessionId, username) ? Data.CODE_OK.Array : Data.CODE_FAILED.Array; break;
        case "/version": buffer = Data.VersionToJson(); break;

        case "/contacts": buffer = DatabaseInstances.users.SerializeContacts(); break;

        case "/barcode39":
            buffer = Protocols.Barcode39.GenerateSvgHandler(parameters);
            ctx.Response.ContentType = "image/svg+xml; charset=utf-8";
            break;

        case "/barcode128":
            buffer = Protocols.Barcode128B.GenerateSvgHandler(parameters);
            ctx.Response.ContentType = "image/svg+xml; charset=utf-8";
            break;

        case "/db/device/list"      : buffer = DatabaseInstances.devices.Serialize(); break;
        case "/db/device/timeline"  : buffer = DatabaseInstances.devices.TimelineHandler(parameters); break;
        case "/db/device/save"      : buffer = DatabaseInstances.devices.SaveHandler(ctx, parameters, username); break;
        case "/db/device/delete"    : buffer = DatabaseInstances.devices.DeleteHandler(parameters, username); break;
        case "/db/device/grid"      : buffer = DatabaseInstances.devices.GridHandler(ctx, username); break;
        case "/db/device/attribute" : buffer = DatabaseInstances.devices.AttributeValue(parameters); break;

        case "/db/user/list"      : buffer = DatabaseInstances.users.Serialize(); break;
        case "/db/user/timeline"  : buffer = DatabaseInstances.users.TimelineHandler(parameters); break;
        case "/db/user/save"      : buffer = DatabaseInstances.users.SaveHandler(ctx, parameters, username); break;
        case "/db/user/delete"    : buffer = DatabaseInstances.users.DeleteHandler(parameters, username); break;
        case "/db/user/grid"      : buffer = DatabaseInstances.users.GridHandler(ctx, username); break;
        case "/db/user/attribute" : buffer = DatabaseInstances.users.AttributeValue(parameters); break;

        case "/db/getentropy" : buffer = Tools.PasswordStrength.GetEntropy(); break;
        case "/db/gandalf"    : buffer = Tools.PasswordStrength.GandalfThreadWrapper(ctx, username); break;

        case "/fetch/networkinfo": buffer = Protocols.Kerberos.NetworkInfo(); break;
        case "/fetch/status"        : buffer = Tools.Fetch.Status(); break;
        case "/fetch/singledevice"  : buffer = Tools.Fetch.SingleDeviceSerialize(parameters, true); break;
        case "/fetch/singleuser"    : buffer = Tools.Fetch.SingleUserSerialize(parameters); break;
        case "/fetch/devices"       : buffer = Tools.Fetch.DevicesTask(parameters, username); break;
        case "/fetch/users"         : buffer = Tools.Fetch.UsersTask(parameters, username); break;
        case "/fetch/import"        : buffer = Tools.Fetch.ImportTask(parameters, username); break;
        case "/fetch/approve"       : buffer = Tools.Fetch.ApproveLastTask(parameters, username); break;
        case "/fetch/abort"         : buffer = Tools.Fetch.CancelTask(username); break;
        case "/fetch/discard"       : buffer = Tools.Fetch.DiscardLastTask(username); break;

        case "/manage/device/wol"      : buffer = Protocols.Wol.Wakeup(parameters); break;
        case "/manage/device/shutdown" : buffer = OperatingSystem.IsWindows() ? Protocols.Wmi.Wmi_Win32PowerHandler(parameters, 12) : null; break;
        case "/manage/device/reboot"   : buffer = OperatingSystem.IsWindows() ? Protocols.Wmi.Wmi_Win32PowerHandler(parameters, 6) : null; break;
        case "/manage/device/logoff"   : buffer = OperatingSystem.IsWindows() ? Protocols.Wmi.Wmi_Win32PowerHandler(parameters, 4) : null; break;
        case "/manage/device/printtest": buffer = Proprietary.Printers.Generic.PrintTestPage(parameters); break;
        //case "/manage/device/getfiles" : buffer = FileBrowser.Get(parameters); break;

        case "/manage/user/unlock" :  buffer = OperatingSystem.IsWindows() ? Protocols.Kerberos.UnlockUser(parameters) : null; break;
        case "/manage/user/enable" :  buffer = OperatingSystem.IsWindows() ? Protocols.Kerberos.EnableUser(parameters) : null; break;
        case "/manage/user/disable": buffer = OperatingSystem.IsWindows() ? Protocols.Kerberos.DisableUser(parameters) : null; break;

        case "/docs/list"   : buffer = Tools.Documentation.List(parameters); break;
        case "/docs/create" : buffer = Tools.Documentation.Create(ctx, username); break;
        case "/docs/delete" : buffer = Tools.Documentation.Delete(parameters, username); break;
        case "/docs/view":
            string acceptEncoding = ctx.Request.Headers.Get("Accept-Encoding")?.ToLower() ?? String.Empty;
            bool acceptGZip = acceptEncoding.Contains("gzip");
            buffer = Tools.Documentation.View(parameters, acceptGZip);
            if (acceptGZip)
                ctx.Response.AddHeader("Content-Encoding", "gzip");
            break;

        case "/debit/list": buffer = Tools.DebitNotes.List(parameters); break;
        case "/debit/view": buffer = Tools.DebitNotes.View(parameters); break;
        case "/debit/create": buffer = Tools.DebitNotes.Create(ctx, username); break;
        case "/debit/delete": buffer = Tools.DebitNotes.Delete(parameters, username); break;
        case "/debit/return": buffer = Tools.DebitNotes.Return(parameters, username); break;
        case "/debit/templates": buffer = Tools.DebitNotes.ListTemplate(); break;
        case "/debit/banners": buffer = Tools.DebitNotes.ListBanners(); break;

        case "/tools/dnslookup"  : buffer = Protocols.Dns.Resolve(parameters); break;
        case "/tools/ntp"        : buffer = Protocols.Ntp.Request(parameters); break;
        case "/tools/locateip"   : buffer = Tools.LocateIp.Locate(ctx); break;
        case "/tools/maclookup"  : buffer = Tools.MacLookup.Lookup(ctx); break;
        case "/tools/downstream" : buffer = Tools.SpeedTest.DownStream(ctx, parameters); break;
        case "/tools/upstream"   : buffer = Tools.SpeedTest.UpStream(ctx, parameters); break;

        case "/wmi/query"      : buffer = OperatingSystem.IsWindows() ? Protocols.Wmi.Query(ctx, parameters) : null; break;
        case "/wmi/killprocess": buffer = OperatingSystem.IsWindows() ? Protocols.Wmi.WmiKillProcess(parameters) : null; break;

        case "/acl/list"     : buffer = Auth.ListUsers(); break;
        case "/acl/create"   : buffer = Auth.CreateUser(parameters, ctx, username); break;
        case "/acl/delete"   : buffer = Auth.DeleteUser(parameters, username); break;
        case "/acl/sessions" : buffer = Auth.ListSessions(); break;
        case "/acl/kickuser" : buffer = Auth.KickUser(parameters, username); break;

        case "/config/zones/list"        : buffer = Tools.Zones.ListZones(); break;
        case "/config/zones/save"        : buffer = Tools.Zones.SaveZones(ctx); break;
        case "/config/smtpprofiles/list" : buffer = Tools.SmtpProfiles.ListEmailProfiles(); break;
        case "/config/smtpprofiles/save" : buffer = Tools.SmtpProfiles.SaveEmailProfile(ctx); break;

        case "/config/checkupdate"       : buffer = Update.CheckLatestRelease(); break;
        case "/config/upload/iplocation" : buffer = Update.LocationFormDataHandler(ctx); break;
        case "/config/upload/proxy"      : buffer = Update.ProxyFormDataHandler(ctx); break;
        case "/config/upload/macresolve" : buffer = Update.MacResolverFormDataHandler(ctx); break;

        case "/log/list": buffer = Logger.List(); break;
        default: return false;
        }
        
        ctx.Response.StatusCode = (int)HttpStatusCode.OK;
        ctx.Response.AddHeader("Length", buffer?.Length.ToString() ?? "0");
        if (buffer != null) ctx.Response.OutputStream.Write(buffer, 0, buffer.Length);

        ctx.Response.Close();
        return true;
    }

    private static bool WebSocketHandler(HttpListenerContext ctx) {
        if (!ctx.Request.IsWebSocketRequest) {
            return false;
        }

        switch (ctx.Request.Url.AbsolutePath) {
        case "/ws/keepalive":
            KeepAlive.WebSocketHandler(ctx);
            return true;

        case "/ws/ping":
            Protocols.Icmp.WebSocketHandler(ctx);
            return true;

        case "/ws/dhcp":
            Protocols.Dhcp.WebSocketHandler(ctx);
            return true;

        case "/ws/portscan":
            Tools.PortScan.WebSocketHandler(ctx);
            return true;

        case "/ws/traceroute":
            Tools.TraceRoute.WebSocketHandler(ctx);
            return true;

        case "/ws/sitecheck":
            Tools.SiteCheck.WebSocketHandler(ctx);
            return true;

        case "/ws/telnet":
            Protocols.Telnet.WebSocketHandler(ctx);
            return true;

        case "/ws/ssh":
            //Ssh.WebSocketHandler(ctx);
            break;

        case "/ws/watchdog":
            //Watchdog.WebSocketHandler(ctx);
            break;

        case "/ws/livestats/device":
            Tools.LiveStats.DeviceStats(ctx);
            return true;

        case "/ws/livestats/user":
            Tools.LiveStats.UserStats(ctx);
            return true;
        }

        return false;
    }

    public override string ToString() {
        string s = string.Empty;
        foreach (string prefix in listener.Prefixes)
            s += (s.Length == 0 ? String.Empty : "\n") + "Listening on " + prefix;
        return s;
    }
}