using System.Net;
using UnityEngine;

namespace MyGame.MyClient
{
    public sealed class ClientBehaviour : MonoBehaviour
    {
        [SerializeField] private GameObject playerPrefab;

        [Header("Server")]
        [SerializeField] private string serverIp = "127.0.0.1";
        [SerializeField] private int serverPort = 7777;

        private ClientHost host;

        private void Awake()
        {
            Application.runInBackground = true;
        }

        private void Start()
        {
            var serverEP = new IPEndPoint(IPAddress.Parse(serverIp), serverPort);

            host = new ClientHost(
                transport: new UdpTransport(),
                input: new UnityInputSource(),
                world: new WorldService(playerPrefab),
                serverEP: serverEP
            );

            host.Start();
        }

        private void Update()
        {
            host?.Tick(Time.deltaTime);
        }

        private void OnApplicationQuit()
        {
            host?.SendDisconnect();
        }

        private void OnDestroy()
        {
            host?.Stop();
            host = null;
        }
    }
}