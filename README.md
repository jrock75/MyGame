🎮 MyGame — Multiplayer Prototype

A server-authoritative multiplayer game built with Unity 6 and a custom .NET 10 UDP server.

This project focuses on learning real-world multiplayer architecture through a structured 120-day development plan.

✨ Features (Current)

✅ Multiplayer player movement

✅ Server-authoritative simulation

✅ Real-time state synchronization

✅ Join / leave detection

✅ Graceful disconnect handling

✅ Server timeout detection

✅ Stable networking foundation

Phase 0 (Networking Foundation) is complete.

🧱 Architecture
MyGame/
  MyClient/   → Unity client
  MyServer/   → .NET server
  Shared/     → Shared networking models (DLL)
Tech Stack
Component	Technology
Client	Unity 6
Server	.NET 10
Shared	.NET Standard 2.1
Protocol	UDP
Model	Server-authoritative

🌐 Networking Model

UDP transport

First byte = PacketType

Client sends input → Server validates → Server broadcasts snapshots

Snapshot IDs + timestamps

Automatic spawn/despawn from authoritative state

Client → Input → Server
Server → Snapshot → Clients
Server → PlayerLeft → Clients
Server → Welcome → Client
⚙️ Robust Networking

Thread-safe server loops

Background receive + main-thread Unity dispatch

Windows UDP connreset mitigation

Timeout-based disconnect detection

Graceful shutdown handling

No locks during network sends

Player lifecycle validation

UDP has no connection state, so the client detects server loss using heartbeat timeouts.

🧠 Architecture Highlights

SOLID-style service separation

Transport abstraction layers

Packet dispatcher pattern

ServerHost / ClientHost orchestration

Assembly definitions in Unity

Shared DLL auto-copy workflow

Networking foundation is stable and ready for gameplay systems.

🚀 Development Status

Phase 0 — Networking Foundation: ✅ Complete
Phase 1 — Gameplay Systems: 🔄 Starting

Planned next features:

Player stats (health / ammo)

HUD integration

Combat systems

Enemy AI

Respawn mechanics

Project follows a structured 120-day roadmap. 

120_day_multiplayer_snippets

🛠 Setup
Requirements

Unity 6

.NET 10 SDK

Visual Studio or Rider

Run Server
dotnet run --project MyServer
Run Client

Open MyClient in Unity and press Play.

⚙️ Development Notes

Shared DLL auto-copies into Unity via post-build step

Unity runs in background:

Application.runInBackground = true;

Git tracks only source files (.cs, assets, config)

🎯 Goals

Learn multiplayer architecture from scratch

Build scalable server-authoritative systems

Reach a playable multiplayer prototype

👤 Author

Jim Sohr

📜 License

Personal learning project.
