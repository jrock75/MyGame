# MyGame Multiplayer Project

## Structure
- `Client/` - Unity project for the player client.
- `Server/` - Console server project (TCP auth + UDP authoritative server).
- `Shared/` - Optional shared classes between client and server (PlayerState, PacketReader/Writer).

## Setup
1. Clone the repo.
2. Open `Client` in Unity.
3. Build and run the server from the `Server` folder.

## Notes
- TCP authentication returns a GUID for each player.
- UDP handles snapshot updates of all connected players.
- The client uses Unity's New Input System for movement.
