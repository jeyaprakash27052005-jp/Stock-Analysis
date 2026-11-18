using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

// Field names match the backend's JSON exactly (snake_case from Postgres columns).
namespace Stormdrop
{
    [Serializable] public class OkResponse { public bool ok; }

    [Serializable] public class RankInfo { public string tier; public int? division; public string label; public int points; public int? nextTierAt; }

    [Serializable] public class PlayerStatsRow { public string mode; public int matches, wins, top10, kills, deaths, headshots; public long damage; }

    [Serializable]
    public class Profile
    {
        public string id, player_tag, nickname, avatar_id, banner_id, region;
        public int level, xp, xpToNext, br_rank_points, cs_rank_points;
        public long gold, diamonds;
        public string guild_id, guild_name, guild_tag;
        public string character_id, pet_id, parachute_id;
        public JObject outfit, weapon_skins;
        public List<string> emotes, linked;
        public RankInfo brRank, csRank;
        public List<PlayerStatsRow> stats;
        public JObject settings;
    }

    [Serializable] public class AuthResponse { public string token; public Profile profile; }

    [Serializable]
    public class CatalogItem
    {
        public string id, type, name, rarity, description;
        public int? price_gold, price_diamonds;
        public bool in_store;
        public JObject metadata;
    }

    [Serializable] public class Catalog { public List<CatalogItem> items; public Dictionary<string, int> diamondPacks; }

    [Serializable] public class InventoryItem { public string item_id, type, name, rarity; public int quantity; }
    [Serializable] public class InventoryResponse { public List<InventoryItem> items; }
    [Serializable] public class BuyResponse { public bool ok; public string itemId, currency; public long balance; }

    [Serializable]
    public class Mission
    {
        public string id, period, title, metric, claimed_at;
        public int target, progress, reward_xp, reward_gold, reward_diamonds, reward_pass_xp;
        public bool complete;
    }
    [Serializable] public class MissionsResponse { public List<Mission> missions; }

    [Serializable] public class PassSeason { public string id, name, ends_at; public int premium_price_diamonds, xp_per_level, max_level; }
    [Serializable] public class PassReward { public int level, quantity; public string track, item_id, name, type, rarity; }
    [Serializable]
    public class PassResponse
    {
        public PassSeason season;
        public int passXp, level;
        public bool premium;
        public List<string> claimed;
        public List<PassReward> rewards;
    }

    [Serializable] public class SpinOdds { public string item_id, name, rarity; public int quantity, weight; public float percent; }
    [Serializable] public class SpinOddsResponse { public List<SpinOdds> odds; }
    [Serializable] public class SpinWin { public string itemId; public int quantity; public bool duplicate; public int convertedGold; }
    [Serializable] public class SpinResponse { public bool ok; public List<SpinWin> wins; }

    [Serializable] public class LoginRewardDef { public int day, quantity; public string item_id, name; }
    [Serializable] public class LoginRewardsResponse { public List<LoginRewardDef> rewards; public int streakDay; public bool canClaim; }

    [Serializable] public class GameEvent { public string id, title, description, type, starts_at, ends_at; }
    [Serializable] public class EventsResponse { public List<GameEvent> events; }

    [Serializable] public class Friend { public string player_tag, nickname, avatar_id, status; public int level, br_rank_points; public bool online; public RankInfo brRank; }
    [Serializable] public class FriendsResponse { public List<Friend> friends; }

    [Serializable] public class PartyMember { public string player_tag, nickname, avatar_id, character_id; public int level; public bool leader; }
    [Serializable] public class Party { public string id, mode; public List<PartyMember> members; }
    [Serializable] public class PartyInvite { public string party_id, from_nickname, from_tag; }
    [Serializable] public class PartyResponse { public Party party; public List<PartyInvite> invites; }

    [Serializable] public class GuildSummary { public string id, name, tag, description; public int level, members, max_members, min_level; public long glory; }
    [Serializable] public class GuildsResponse { public List<GuildSummary> guilds; }
    [Serializable] public class GuildMember { public string player_tag, nickname, role; public int level, br_rank_points; }
    [Serializable] public class ChatMessage { public long id; public string body, created_at, nickname; }
    [Serializable] public class MyGuildResponse { public GuildSummary guild; public List<GuildMember> members; public List<ChatMessage> chat; }
    [Serializable] public class CreateGuildResponse { public bool ok; public GuildSummary guild; }

    [Serializable] public class LeaderRow { public string player_tag, nickname, guild_tag; public int level, br_rank_points, region_rank; public RankInfo rank; }
    [Serializable] public class LeaderboardResponse { public string region; public List<LeaderRow> players; }

    [Serializable] public class MatchInfo { public string id, mode, mapId, timeOfDay, host; public int port, team; }
    [Serializable]
    public class MatchmakingStatus
    {
        public string status, ticketId, joinTicket;   // status: waiting | matched | cancelled | expired | none | offline
        public int waitedSec, playersInQueue;
        public MatchInfo match;
    }

    [Serializable] public class MatchHistoryRow { public string id, mode, ended_at; public bool is_ranked; public int placement, kills, damage, rank_delta, xp_gained, gold_gained; }
    [Serializable] public class MatchHistoryResponse { public List<MatchHistoryRow> matches; }

    [Serializable] public class IapResponse { public bool ok, duplicate; public int diamonds; public long balance; }

    // Dedicated server
    [Serializable] public class ServerRegisterResponse { public string serverId; }
    [Serializable] public class AllocationPlayer { public string player_id; public int team; }
    [Serializable] public class ServerAllocation { public string matchId, mode, mapId, timeOfDay; public bool isRanked; public List<AllocationPlayer> players; }
    [Serializable] public class ServerHeartbeatResponse { public string status; public ServerAllocation allocation; }
}
