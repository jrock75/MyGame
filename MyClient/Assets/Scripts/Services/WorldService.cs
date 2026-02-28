using System;
using System.Collections.Generic;
using UnityEngine;

namespace MyGame.MyClient
{
    public sealed class WorldService
    {
        private readonly GameObject playerPrefab;
        private readonly Dictionary<Guid, GameObject> spawnedPlayers = new();

        public WorldService(GameObject playerPrefab)
        {
            this.playerPrefab = playerPrefab;
        }

        public void UpsertPlayer(Guid id, float x, float y, float rot)
        {
            if (!spawnedPlayers.TryGetValue(id, out var go) || go == null)
            {
                if (playerPrefab == null)
                {
                    Debug.LogWarning("playerPrefab is not assigned on Client.");
                    return;
                }

                go = UnityEngine.Object.Instantiate(playerPrefab);
                go.name = $"Player_{id}";
                spawnedPlayers[id] = go;
            }

            go.transform.SetPositionAndRotation(
                new Vector3(x, 0f, y),
                Quaternion.Euler(0f, rot, 0f)
            );
        }

        public void RemovePlayer(Guid id)
        {
            if (spawnedPlayers.TryGetValue(id, out var go) && go != null)
                UnityEngine.Object.Destroy(go);

            spawnedPlayers.Remove(id);
        }

        public void DespawnNotIn(HashSet<Guid> seen)
        {
            var toRemove = new List<Guid>();

            foreach (var kvp in spawnedPlayers)
            {
                if (!seen.Contains(kvp.Key))
                    toRemove.Add(kvp.Key);
            }

            foreach (var id in toRemove)
                RemovePlayer(id);
        }

        public void CleanupAll()
        {
            foreach (var kvp in spawnedPlayers)
            {
                if (kvp.Value == null) continue;

#if UNITY_EDITOR
                UnityEngine.Object.DestroyImmediate(kvp.Value);
#else
                UnityEngine.Object.Destroy(kvp.Value);
#endif
            }

            spawnedPlayers.Clear();
        }
    }
}