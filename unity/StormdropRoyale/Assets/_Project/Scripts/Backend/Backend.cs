using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Stormdrop
{
    /// <summary>Typed calls for every backend endpoint the game uses, plus a cached profile and catalog.</summary>
    public static class Backend
    {
        public static Profile Me { get; private set; }
        public static Catalog Catalog { get; private set; }
        public static event Action ProfileChanged;

        static void SetMe(Profile p) { if (p == null) return; Me = p; ProfileChanged?.Invoke(); }

        public static async Task<Profile> RefreshProfile() { SetMe(await ApiClient.Get<Profile>("/me")); return Me; }

        public static async Task<Catalog> LoadCatalog(bool force = false)
        {
            if (Catalog == null || force) Catalog = await ApiClient.Get<Catalog>("/store/catalog");
            return Catalog;
        }

        public static CatalogItem Item(string id) => Catalog?.items?.Find(i => i.id == id);

        // ----- profile
        public static async Task Rename(string nickname) => SetMe(await ApiClient.Post<Profile>("/me/rename", new { nickname }));
        public static async Task SetLoadout(string characterId, string petId) =>
            SetMe(await ApiClient.Put<Profile>("/me/loadout", new { characterId, petId, parachuteId = Me?.parachute_id }));
        public static Task SaveSettings(GameSettingsData data) => ApiClient.Put<OkResponse>("/me/settings", data);
        public static Task RegisterPushToken(string token, string platform) => ApiClient.Post<OkResponse>("/me/push-token", new { token, platform });
        public static Task DeleteAccount() => ApiClient.Delete<OkResponse>("/me");

        // ----- store
        public static Task<InventoryResponse> Inventory() => ApiClient.Get<InventoryResponse>("/store/inventory");
        public static async Task Buy(string itemId, string currency)
        {
            await ApiClient.Post<BuyResponse>("/store/buy", new { itemId, currency });
            await RefreshProfile();
        }
        public static Task<IapResponse> VerifyGooglePurchase(string productId, string purchaseToken) =>
            ApiClient.Post<IapResponse>("/store/iap/google", new { productId, purchaseToken });

        // ----- progression
        public static Task<MissionsResponse> Missions() => ApiClient.Get<MissionsResponse>("/progression/missions");
        public static Task ClaimMission(string id) => ApiClient.Post<OkResponse>($"/progression/missions/{id}/claim");
        public static Task<PassResponse> Pass() => ApiClient.Get<PassResponse>("/progression/pass");
        public static Task BuyPremiumPass() => ApiClient.Post<OkResponse>("/progression/pass/premium");
        public static Task ClaimPass(int level, string track) => ApiClient.Post<OkResponse>("/progression/pass/claim", new { level, track });
        public static Task<SpinOddsResponse> SpinOdds(string eventId) => ApiClient.Get<SpinOddsResponse>($"/progression/spin/{eventId}/odds");
        public static Task<SpinResponse> Spin(string eventId, int count) => ApiClient.Post<SpinResponse>($"/progression/spin/{eventId}", new { count });
        public static Task<LoginRewardsResponse> LoginRewards() => ApiClient.Get<LoginRewardsResponse>("/progression/login-rewards");
        public static Task ClaimLoginReward() => ApiClient.Post<OkResponse>("/progression/login-rewards/claim");
        public static Task<EventsResponse> Events() => ApiClient.Get<EventsResponse>("/progression/events");

        // ----- social
        public static Task<FriendsResponse> Friends() => ApiClient.Get<FriendsResponse>("/social/friends");
        public static Task AddFriend(string playerTag) => ApiClient.Post<OkResponse>("/social/friends/request", new { playerTag });
        public static Task RespondFriend(string playerTag, bool accept) => ApiClient.Post<OkResponse>("/social/friends/respond", new { playerTag, accept });
        public static Task<PartyResponse> Party() => ApiClient.Get<PartyResponse>("/social/party");
        public static Task InviteToParty(string playerTag) => ApiClient.Post<OkResponse>("/social/party/invite", new { playerTag });
        public static Task JoinParty(string partyId) => ApiClient.Post<OkResponse>("/social/party/join", new { partyId });
        public static Task LeaveParty() => ApiClient.Post<OkResponse>("/social/party/leave");
        public static Task<GuildsResponse> SearchGuilds(string search) => ApiClient.Get<GuildsResponse>("/social/guilds?search=" + Uri.EscapeDataString(search ?? ""));
        public static Task<MyGuildResponse> MyGuild() => ApiClient.Get<MyGuildResponse>("/social/guilds/mine");
        public static Task<CreateGuildResponse> CreateGuild(string name, string tag, string description) =>
            ApiClient.Post<CreateGuildResponse>("/social/guilds", new { name, tag, description });
        public static Task JoinGuild(string id) => ApiClient.Post<OkResponse>($"/social/guilds/{id}/join");
        public static Task LeaveGuild() => ApiClient.Post<OkResponse>("/social/guilds/leave");
        public static Task SendChat(string channel, string body) => ApiClient.Post<OkResponse>("/social/chat", new { channel, body });
        public static Task<LeaderboardResponse> Leaderboard(string region) => ApiClient.Get<LeaderboardResponse>("/leaderboard?region=" + region);
        public static Task Report(string playerTag, string matchId, string reason, string details) =>
            ApiClient.Post<OkResponse>("/reports", new { playerTag, matchId, reason, details });

        // ----- matchmaking
        public static Task<MatchmakingStatus> JoinQueue(GameMode mode, bool ranked) =>
            ApiClient.Post<MatchmakingStatus>("/matchmaking/join", new { mode = GameConstants.ApiMode(mode), ranked });
        public static Task<MatchmakingStatus> QueueStatus(string ticketId) => ApiClient.Get<MatchmakingStatus>("/matchmaking/" + ticketId);
        public static Task<MatchmakingStatus> CurrentTicket() => ApiClient.Get<MatchmakingStatus>("/matchmaking/current");
        public static Task CancelQueue(string ticketId) => ApiClient.Delete<OkResponse>("/matchmaking/" + ticketId);
        public static Task<MatchHistoryResponse> MatchHistory() => ApiClient.Get<MatchHistoryResponse>("/match-history");
    }
}
