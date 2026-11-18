using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Stormdrop
{
    public enum PingType : byte { Location, Enemy, Loot, Vehicle, Danger }

    /// <summary>Team map pings and quick-chat lines, relayed by the server to teammates only.</summary>
    public class PingSystem : NetworkBehaviour
    {
        public struct Ping { public Vector3 position; public PingType type; public string from; public float until; }

        public static readonly List<Ping> Active = new List<Ping>();
        public static event System.Action<string, string> QuickChatReceived;   // from, text

        public static readonly string[] QuickChatLines =
        {
            "Enemy spotted!", "Need medkit", "Need ammo", "Follow me", "Wait for me", "Go go go!",
            "Get in the vehicle", "Revive me!", "Good job!", "Zone is coming, move!",
        };

        PlayerIdentity _id;
        float _lastPing;

        void Awake() => _id = GetComponent<PlayerIdentity>();

        public void SendPing(Vector3 pos, PingType type)
        {
            if (!IsOwner || Time.time - _lastPing < 0.75f) return;
            _lastPing = Time.time;
            PingRpc(pos, type);
        }

        public void SendQuickChat(int index) { if (IsOwner) QuickChatRpc((byte)index); }

        [Rpc(SendTo.Server)]
        void PingRpc(Vector3 pos, PingType type, RpcParams rpc = default)
        {
            if (rpc.Receive.SenderClientId != OwnerClientId) return;
            foreach (var p in PlayerIdentity.All)
                if (p.Team.Value == _id.Team.Value) ShowPingRpc(pos, type, RpcTarget.Single(p.OwnerClientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.Server)]
        void QuickChatRpc(byte index, RpcParams rpc = default)
        {
            if (rpc.Receive.SenderClientId != OwnerClientId || index >= QuickChatLines.Length) return;
            foreach (var p in PlayerIdentity.All)
                if (p.Team.Value == _id.Team.Value) ShowQuickChatRpc(index, RpcTarget.Single(p.OwnerClientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void ShowPingRpc(Vector3 pos, PingType type, RpcParams rpc = default)
        {
            Active.Add(new Ping { position = pos, type = type, from = _id.Nickname.Value.ToString(), until = Time.time + 12f });
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void ShowQuickChatRpc(byte index, RpcParams rpc = default) =>
            QuickChatReceived?.Invoke(_id.Nickname.Value.ToString(), QuickChatLines[index]);

        void LateUpdate()
        {
            if (IsOwner) Active.RemoveAll(p => p.until < Time.time);
        }
    }
}
