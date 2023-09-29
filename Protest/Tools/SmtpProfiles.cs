﻿using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Protest.Tools;
internal class SmtpProfiles {
    public record SmtpProfile {
        public string server;
        public int port;
        public string sender;
        public string username;
        public string password;
        public string recipients;
        public bool ssl;
        public Guid guid;
    }


    public static byte[] ListEmailProfiles() {
        if (!File.Exists(Data.FILE_EMAIL_PROFILES)) return "[]"u8.ToArray();

        try {
            byte[] json = File.ReadAllBytes(Data.FILE_EMAIL_PROFILES);

            JsonSerializerOptions options = new JsonSerializerOptions();
            options.Converters.Add(new EmailProfilesJsonConverter(true));

            SmtpProfile[] profiles = JsonSerializer.Deserialize<SmtpProfile[]>(json, options);
            json = JsonSerializer.SerializeToUtf8Bytes(profiles, options);

            return json;
        }
        catch {
            return Data.CODE_FAILED.Array;
        }
    }

    public static byte[] SaveEmailProfile(HttpListenerContext ctx) {
        string payload;
        using (StreamReader reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding))
            payload = reader.ReadToEnd();

        JsonSerializerOptions options = new JsonSerializerOptions();
        options.Converters.Add(new EmailProfilesJsonConverter(false));

        SmtpProfile[] oldProfiles;
        try {
            byte[] bytes = File.ReadAllBytes(Data.FILE_EMAIL_PROFILES);
            oldProfiles = JsonSerializer.Deserialize<SmtpProfile[]>(bytes, options);
        }
        catch {
            oldProfiles = Array.Empty<SmtpProfile>();
        }

        SmtpProfile[] newProfiles = JsonSerializer.Deserialize<SmtpProfile[]>(payload, options);

        for (int i = 0; i < newProfiles.Length; i++) {
            if (newProfiles[i].guid == default) {

                newProfiles[i].guid = Guid.NewGuid();
            }

            if (newProfiles[i].password?.Length > 0) continue;

            SmtpProfile old = null;
            for (int j = 0; j < oldProfiles.Length; j++) {
                if (newProfiles[i].guid == oldProfiles[j].guid) {
                    old = oldProfiles[j];
                    break;
                }
            }

            if (old is not null) {
                newProfiles[i].password = old.password;
            }
        }

        try {
            byte[] file = JsonSerializer.SerializeToUtf8Bytes(newProfiles, options);
            File.WriteAllBytes(Data.FILE_EMAIL_PROFILES, file);
        }
        catch {
            return Data.CODE_FAILED.Array;
        }


        return Data.CODE_OK.Array;
    }

}

internal sealed class EmailProfilesJsonConverter : JsonConverter<SmtpProfiles.SmtpProfile[]> {
    private bool hidePasswords;
    public EmailProfilesJsonConverter(bool hidePasswords) {
        this.hidePasswords = hidePasswords;
    }

    public override SmtpProfiles.SmtpProfile[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        List<SmtpProfiles.SmtpProfile> profiles = new List<SmtpProfiles.SmtpProfile>();

        if (reader.TokenType != JsonTokenType.StartArray) {
            throw new JsonException("Expected start of an array.");
        }

        while (reader.Read()) {
            if (reader.TokenType == JsonTokenType.EndArray) {
                break;
            }

            if (reader.TokenType == JsonTokenType.StartObject) {
                SmtpProfiles.SmtpProfile profile = new SmtpProfiles.SmtpProfile();

                while (reader.Read()) {
                    if (reader.TokenType == JsonTokenType.EndObject) {
                        break;
                    }

                    if (reader.TokenType == JsonTokenType.PropertyName) {
                        string propertyName = reader.GetString();
                        reader.Read();

                        switch (propertyName) {
                        case "server": profile.server = reader.GetString(); break;
                        case "port": profile.port = reader.GetInt32(); break;
                        case "sender": profile.sender = reader.GetString(); break;
                        case "username": profile.username = reader.GetString(); break;
                        case "password": profile.password = hidePasswords ? string.Empty : reader.GetString(); break;
                        case "recipients": profile.recipients = reader.GetString(); break;
                        case "ssl": profile.ssl = reader.GetBoolean(); break;
                        case "guid": profile.guid = reader.GetGuid(); break;
                        default: reader.Skip(); break;
                        }
                    }
                }

                profiles.Add(profile);
            }
        }

        return profiles.ToArray();
    }

    public override void Write(Utf8JsonWriter writer, SmtpProfiles.SmtpProfile[] value, JsonSerializerOptions options) {
        ReadOnlySpan<byte> _server = "server"u8;
        ReadOnlySpan<byte> _port = "port"u8;
        ReadOnlySpan<byte> _sender = "sender"u8;
        ReadOnlySpan<byte> _username = "username"u8;
        ReadOnlySpan<byte> _password = "password"u8;
        ReadOnlySpan<byte> _recipients = "recipients"u8;
        ReadOnlySpan<byte> _ssl = "ssl"u8;
        ReadOnlySpan<byte> _guid = "guid"u8;

        writer.WriteStartArray();

        for (int i = 0; i < value.Length; i++) {
            writer.WriteStartObject();
            writer.WriteString(_server, value[i].server);
            writer.WriteNumber(_port, value[i].port);
            writer.WriteString(_sender, value[i].sender);
            writer.WriteString(_username, value[i].username);
            writer.WriteString(_password, hidePasswords ? string.Empty : value[i].password);
            writer.WriteString(_recipients, value[i].recipients);
            writer.WriteBoolean(_ssl, value[i].ssl);
            writer.WriteString(_guid, value[i].guid);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }
}