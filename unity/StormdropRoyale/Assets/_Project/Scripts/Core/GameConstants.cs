namespace Stormdrop
{
    public enum GameMode : byte { BrSolo, BrDuo, BrSquad, ClashSquad, TeamDeathmatch, Training }

    public enum PlayerLifeState : byte { InPlane, Parachuting, Alive, Knocked, Dead, Spectating }

    public enum Stance : byte { Standing, Crouching, Prone }

    public static class GameConstants
    {
        public const string GameName = "Stormdrop Royale";
        public const string VictoryTitle = "STORM CHAMPION!";
        public const string Version = "1.0.0";

        public const string BootstrapScene = "Bootstrap";
        public const string GameScene = "IsleVerdant";

        public const float MapSize = 4000f;          // 4 x 4 km, centred on (0,0)
        public const float WaterLevel = 4f;
        public const int MaxPlayers = 50;
        public const float MaxHealth = 200f;
        public const float MaxEnergy = 200f;         // "Vigor": converts slowly into HP
        public const float KnockHealth = 100f;       // bleed-out pool while knocked
        public const float ReviveSeconds = 5f;
        public const ushort DefaultServerPort = 7777;
        public const int TickRate = 30;

        // Physics layers (created by the setup wizard)
        public const int LayerPlayer = 8;
        public const int LayerVehicle = 9;
        public const int LayerLoot = 10;
        public const int LayerEnvironment = 0;       // Default

        public static readonly int EnvironmentMask = 1 << 0;
        public static readonly int ShootableMask = (1 << 0) | (1 << LayerPlayer) | (1 << LayerVehicle);

        public static string ApiMode(GameMode m) => m switch
        {
            GameMode.BrSolo => "br_solo",
            GameMode.BrDuo => "br_duo",
            GameMode.BrSquad => "br_squad",
            GameMode.ClashSquad => "clash_squad",
            GameMode.TeamDeathmatch => "tdm",
            _ => "training",
        };

        public static GameMode ParseMode(string s) => s switch
        {
            "br_solo" => GameMode.BrSolo,
            "br_duo" => GameMode.BrDuo,
            "br_squad" => GameMode.BrSquad,
            "clash_squad" => GameMode.ClashSquad,
            "tdm" => GameMode.TeamDeathmatch,
            _ => GameMode.Training,
        };

        public static int TeamSize(GameMode m) => m switch
        {
            GameMode.BrDuo => 2,
            GameMode.BrSquad => 4,
            GameMode.ClashSquad => 4,
            GameMode.TeamDeathmatch => 4,
            _ => 1,
        };

        /// <summary>Converts a string to a FixedString32Bytes, trimming so the UTF-8 bytes fit (max 29).</summary>
        public static Unity.Collections.FixedString32Bytes Fs32(string s)
        {
            s ??= "";
            while (System.Text.Encoding.UTF8.GetByteCount(s) > 29) s = s.Substring(0, s.Length - 1);
            return new Unity.Collections.FixedString32Bytes(s);
        }

        public static bool IsBattleRoyale(GameMode m) => m == GameMode.BrSolo || m == GameMode.BrDuo || m == GameMode.BrSquad;
        public static bool UsesKnockdown(GameMode m) => m == GameMode.BrDuo || m == GameMode.BrSquad || m == GameMode.ClashSquad;
    }

    /// <summary>Tiny deterministic RNG (xorshift) so server and clients generate identical loot from one seed.</summary>
    public struct DeterministicRandom
    {
        uint _state;
        public DeterministicRandom(int seed) { _state = (uint)seed ^ 0x9E3779B9u; if (_state == 0) _state = 1; }
        public uint NextUInt() { _state ^= _state << 13; _state ^= _state >> 17; _state ^= _state << 5; return _state; }
        public float Value() => (NextUInt() & 0xFFFFFF) / (float)0x1000000;
        public int Range(int minInclusive, int maxExclusive) => minInclusive + (int)(NextUInt() % (uint)(maxExclusive - minInclusive));
        public float Range(float min, float max) => min + (max - min) * Value();
    }
}
