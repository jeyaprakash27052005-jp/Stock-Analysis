using Unity.Netcode.Components;
using UnityEngine;

namespace Stormdrop
{
    /// <summary>
    /// Owner-authoritative transform so movement feels instant on high-ping mobile networks.
    /// The server still validates speed every tick (PlayerController.ServerValidateMovement).
    /// </summary>
    [DisallowMultipleComponent]
    public class ClientNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
