using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Security;
using System.Xml.Linq;
using Microsoft.Bot.Connector;
using Newtonsoft.Json.Linq;

namespace ZimbraBOT
{
    public class ContextConstants
    {
        public const string PersistedCookieKey = "persistedCookie";
        public const string AuthResultKey = "authResult";
        public const string MagicNumberKey = "authMagicNumber";
        public const string MagicNumberValidated = "authMagicNumberValidated";
        public const int MagicNumberLength = 4;

        public const string AuthTokenKey = "authTokenKey";
        public const string Zimbar = "zimbra";
        public const string MailServer = "mailServer";
        public const string MailServerPort = "mailServerPort";
        public const string MailServerUseSecure = "mailServerUseSecure";
        public const string Email = "email";
        public const string BOT = "bot";

        public static readonly ConcurrentDictionary<string, JObject> StateSession =
            new ConcurrentDictionary<string, JObject>();

        public static string EncryptAuthMagicNumber(string authMagicNumber)
        {
            var bytes = MachineKey.Protect(Encoding.UTF8.GetBytes(authMagicNumber), BOT);
            return Convert.ToBase64String(bytes);
        }

        public static string DecryptAuthMagicNumber(string encryptBase64)
        {
            var encryptBytes = Convert.FromBase64String(encryptBase64);
            var bytes = MachineKey.Unprotect(encryptBytes, BOT);
            return Encoding.UTF8.GetString(bytes);
        }
    }

    public class StateSessions
    {
        private StateSessions() { }

        public static ConcurrentDictionary<string, JObject> Instance { get; } 
            = new ConcurrentDictionary<string, JObject>();


        public static void SaveUserPayload(string authMagicNumber, JObject payload)
        {
            Instance.TryAdd(authMagicNumber, payload);
        }


        public static JObject GetUserPayloadByKey(string authMagicNumber)
        {
            JObject payload;
            Instance.TryGetValue(authMagicNumber, out payload);
            return payload;
        }


        public static JObject GetUserPayloadByUserId(string userId, string channelId)
        {
            var userItem = Instance.FirstOrDefault(u => (string)u.Value["userId"] == userId && (string)u.Value["channelId"] == channelId);
            return userItem.Value;
        }
    }

    public class State
    {
        public string UserId { get; set; }
        public ConversationReference ConversationReference { get; set; }
    }
}

