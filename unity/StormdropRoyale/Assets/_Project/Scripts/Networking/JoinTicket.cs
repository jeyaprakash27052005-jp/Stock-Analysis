using System;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Stormdrop
{
    /// <summary>
    /// The backend signs a short-lived HS256 JWT ("join ticket") when matchmaking succeeds.
    /// The dedicated server verifies it with the shared SERVER_SECRET, so only players the
    /// backend matched can join, with the team/character the backend assigned.
    /// </summary>
    public class JoinTicket
    {
        public string PlayerId;
        public string MatchId;
        public int Team;
        public string Nickname;
        public string CharacterId;
        public string PetId;
        public bool Offline;

        public static string MakeOffline(string nickname, string characterId, string petId) =>
            $"offline|{nickname}|{characterId}|{petId}";

        public static bool TryParseOffline(string raw, out JoinTicket t)
        {
            t = null;
            if (raw == null || !raw.StartsWith("offline|")) return false;
            var p = raw.Split('|');
            t = new JoinTicket { Offline = true, PlayerId = "local", MatchId = "training", Team = 0,
                                 Nickname = p.Length > 1 ? p[1] : "Player", CharacterId = p.Length > 2 ? p[2] : "char_kael",
                                 PetId = p.Length > 3 ? p[3] : "" };
            return true;
        }

        public static bool TryVerify(string jwt, string secret, out JoinTicket ticket, out string error)
        {
            ticket = null; error = null;
            try
            {
                var parts = jwt.Split('.');
                if (parts.Length != 3) { error = "malformed"; return false; }
                using (var h = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
                {
                    var expected = h.ComputeHash(Encoding.UTF8.GetBytes(parts[0] + "." + parts[1]));
                    if (!FixedTimeEquals(expected, FromBase64Url(parts[2]))) { error = "bad signature"; return false; }
                }
                var header = JObject.Parse(Encoding.UTF8.GetString(FromBase64Url(parts[0])));
                if ((string)header["alg"] != "HS256") { error = "bad alg"; return false; }
                var p = JObject.Parse(Encoding.UTF8.GetString(FromBase64Url(parts[1])));
                long exp = (long?)p["exp"] ?? 0;
                if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp) { error = "expired"; return false; }
                ticket = new JoinTicket
                {
                    PlayerId = (string)p["sub"], MatchId = (string)p["mid"], Team = (int?)p["team"] ?? 0,
                    Nickname = (string)p["nick"] ?? "Player", CharacterId = (string)p["chr"] ?? "char_kael", PetId = (string)p["pet"] ?? "",
                };
                return !string.IsNullOrEmpty(ticket.PlayerId);
            }
            catch (Exception e) { error = e.Message; return false; }
        }

        static byte[] FromBase64Url(string s)
        {
            s = s.Replace('-', '+').Replace('_', '/');
            switch (s.Length % 4) { case 2: s += "=="; break; case 3: s += "="; break; }
            return Convert.FromBase64String(s);
        }

        static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
