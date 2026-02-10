using System;
using UnityEngine;

namespace MyServer.GameLogic
{
    [Serializable]
    public class PlayerState
    {
        // Unique identity for the player
        public Guid PlayerGuid;

        // Display name (optional)
        public string playerName = "Player";

        // Unity types
        public Vector3 position = Vector3.zero;
        public Vector3 velocity = Vector3.zero;
        public Quaternion rotation = Quaternion.identity;

        // Alive/dead status
        public bool isAlive = true;
    }
}