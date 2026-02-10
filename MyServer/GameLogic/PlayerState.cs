using System;
using System.Numerics;

namespace MyServer.GameLogic
{
    [Serializable]
    public class PlayerState
    {
        // --------------------
        // Unique identifiers
        // --------------------
        public Guid PlayerGuid;      // GUID assigned by TCP auth
        public string playerName;    // Player's display name

        // --------------------
        // Transform / movement
        // --------------------
        public Vector3 position = Vector3.Zero;
        public Quaternion rotation = Quaternion.Identity;
        public Vector3 velocity = Vector3.Zero;

        // --------------------
        // Gameplay state
        // --------------------
        public bool isAlive = true;

        // --------------------
        // Optional helper methods
        // --------------------
        public PlayerState Clone()
        {
            return new PlayerState
            {
                PlayerGuid = this.PlayerGuid,
                playerName = this.playerName,
                position = this.position,
                rotation = this.rotation,
                velocity = this.velocity,
                isAlive = this.isAlive
            };
        }
    }
}