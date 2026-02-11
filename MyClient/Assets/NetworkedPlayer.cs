using MyGame.Shared;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class NetworkedPlayer : MonoBehaviour
{
    [Header("Network Settings")]
    public string tcpServerIP = "127.0.0.1";
    public int tcpPort = 9000;
    public string udpServerIP = "127.0.0.1";
    public int udpPort = 7777;
    public float tickRate = 30f;
    public string playerName = "UnityPlayer";

    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 720f;
    public GameObject remotePlayerPrefab;

    private Guid myPlayerGuid = Guid.Empty;
    private UdpClient udp;
    private IPEndPoint serverEP;
    private float tickTimer = 0f;
    private readonly ConcurrentQueue<PlayerState> packetQueue = new();
    private Rigidbody rb;

    private PlayerState predictedState;
    private readonly Dictionary<Guid, PlayerState> playerStates = new();
    private readonly Dictionary<Guid, GameObject> remotePlayers = new();

    private PlayerControls controls;
    private Vector2 moveInput;

    void Awake()
    {
        controls = new PlayerControls();
        controls.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Player.Move.canceled += ctx => moveInput = Vector2.zero;
    }

    void OnEnable() => controls.Enable();
    void OnDisable() => controls.Disable();

    async void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        Application.runInBackground = true;

        // Step 1: TCP auth
        try
        {
            myPlayerGuid = await TcpAuthenticate(tcpServerIP, tcpPort, playerName, "password123");
            Debug.Log($"Received session GUID: {myPlayerGuid}");
        }
        catch (Exception ex)
        {
            Debug.LogError("TCP Auth failed: " + ex.Message);
            enabled = false;
            return;
        }

        // Step 2: UDP setup
        udp = new UdpClient();
        serverEP = new IPEndPoint(IPAddress.Parse(udpServerIP), udpPort);

        predictedState = new PlayerState
        {
            PlayerGuid = myPlayerGuid,
            playerName = playerName,
            position = NetConversions.ToNumerics(transform.position),
            rotation = NetConversions.ToNumerics(transform.rotation),
            velocity = NetConversions.ToNumerics(Vector3.zero),
            isAlive = true
        };

        StartReceivingLoop();
    }

    void FixedUpdate() => HandleMovement();

    void Update()
    {
        tickTimer += Time.deltaTime;
        if (tickTimer >= 1f / tickRate)
        {
            tickTimer = 0f;
            SendPlayerState();
            ProcessPacketQueue();
            UpdateRemotePlayers();
            ApplyPredictionCorrection();
        }
    }

    #region Movement
    private void HandleMovement()
    {
        Vector3 moveDir = new Vector3(moveInput.x, 0f, moveInput.y);
        if (moveDir.sqrMagnitude > 0.001f)
        {
            Vector3 move = moveDir.normalized * moveSpeed * Time.fixedDeltaTime;
            rb.MovePosition(rb.position + move);

            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, targetRot, rotationSpeed * Time.fixedDeltaTime));

            predictedState.position = NetConversions.ToNumerics(rb.position);
            predictedState.rotation = NetConversions.ToNumerics(rb.rotation);
        }
    }
    #endregion

    #region Networking
    private void SendPlayerState()
    {
        using var writer = new PacketWriter();
        writer.WriteGuid(predictedState.PlayerGuid);
        writer.WritePlayerState(predictedState);
        try
        {
            udp.Send(writer.ToArray(), writer.Length, serverEP);
        }
        catch { /* ignore send errors */ }
    }

    private async void StartReceivingLoop()
    {
        try
        {
            while (true)
            {
                UdpReceiveResult result;
                try { result = await udp.ReceiveAsync(); }
                catch (ObjectDisposedException) { break; }
                catch (SocketException) { continue; }

                using var reader = new PacketReader(result.Buffer);

                // Optionally: read recipient GUID + snapshotId
                Guid recipientGuid = reader.ReadGuid();
                int snapshotId = reader.ReadInt32();

                while (reader.Position < reader.Length)
                {
                    var state = reader.ReadPlayerState();
                    if (state != null)
                        packetQueue.Enqueue(state);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.Log("UDP receive loop stopped: " + ex.Message);
        }
    }

    private void ProcessPacketQueue()
    {
        while (packetQueue.TryDequeue(out var state))
        {
            if (state.PlayerGuid == myPlayerGuid)
            {
                predictedState = state;
                continue;
            }

            // Spawn remote player if missing
            if (!remotePlayers.ContainsKey(state.PlayerGuid))
            {
                GameObject go = Instantiate(remotePlayerPrefab);
                remotePlayers[state.PlayerGuid] = go;
            }

            playerStates[state.PlayerGuid] = state;
        }
    }

    private void UpdateRemotePlayers()
    {
        List<Guid> toRemove = new();
        foreach (var kvp in remotePlayers)
            if (!playerStates.ContainsKey(kvp.Key))
            {
                Destroy(kvp.Value);
                toRemove.Add(kvp.Key);
            }
        foreach (var guid in toRemove) remotePlayers.Remove(guid);

        foreach (var kvp in playerStates)
        {
            if (kvp.Key == myPlayerGuid) continue;
            GameObject go = remotePlayers[kvp.Key];
            go.transform.position = Vector3.Lerp(go.transform.position, NetConversions.ToUnity(kvp.Value.position), 10f * Time.deltaTime);
            go.transform.rotation = Quaternion.Slerp(go.transform.rotation, NetConversions.ToUnity(kvp.Value.rotation), 10f * Time.deltaTime);
        }
    }

    private void ApplyPredictionCorrection()
    {
        if (playerStates.TryGetValue(myPlayerGuid, out var serverState))
        {
            if (Vector3.Distance(transform.position, NetConversions.ToUnity(serverState.position)) > 0.05f)
            {
                transform.position = Vector3.Lerp(transform.position, NetConversions.ToUnity(serverState.position), 0.2f);
                transform.rotation = Quaternion.Slerp(transform.rotation, NetConversions.ToUnity(serverState.rotation), 0.2f);
            }
        }
    }

    private void OnApplicationQuit()
    {
        udp.Close();
        udp.Dispose();
    }
    #endregion

    #region TCP Auth Helper
    private async Task<Guid> TcpAuthenticate(string serverIP, int port, string username, string password)
    {
        using TcpClient client = new TcpClient();
        await client.ConnectAsync(serverIP, port);
        var stream = client.GetStream();

        string request = $"{username}:{password}\n";
        byte[] reqBytes = System.Text.Encoding.UTF8.GetBytes(request);
        await stream.WriteAsync(reqBytes, 0, reqBytes.Length);

        byte[] guidBytes = new byte[16];
        int read = await stream.ReadAsync(guidBytes, 0, 16);
        if (read != 16) throw new Exception("Invalid response from auth server");

        return new Guid(guidBytes);
    }
    #endregion
}
