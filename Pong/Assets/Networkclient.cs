using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

public class NetworkClient : MonoBehaviour
{
    [Header("Servidor")]
    [SerializeField] private string serverIp = "127.0.0.1";
    [SerializeField] private int serverPort = 7777;

    [Header("Objetos do jogo")]
    [SerializeField] private Transform ball;
    [SerializeField] private Transform paddle1;
    [SerializeField] private Transform paddle2;

    [Header("UI")]
    [SerializeField] private Text scorePlayer1Text;
    [SerializeField] private Text scorePlayer2Text;
    [SerializeField] private Text player1IdText;
    [SerializeField] private Text player2IdText;
    [SerializeField] private Text winnerText;

    private UdpClient udpClient;
    private Thread receiveThread;

    private readonly object stateLock = new object();

    private byte myPlayerId = 0;
    private bool connected = false;
    private bool shuttingDown = false;

    // Estado recebido do servidor
    private Vector2 ballPos = Vector2.zero;

    private float paddle1Y = 0f;
    private float paddle2Y = 0f;

    private int score1 = 0;
    private int score2 = 0;

    private bool gameOver = false;
    private byte winner = 0;

    private void Start()
    {
        try
        {
            IPEndPoint serverEndpoint =
                new IPEndPoint(
                    IPAddress.Parse(serverIp),
                    serverPort
                );

            udpClient = new UdpClient(0);

            udpClient.Connect(serverEndpoint);

            connected = true;

            Debug.Log("=================================");
            Debug.Log("CLIENTE UDP INICIADO");
            Debug.Log("Servidor: " + serverIp);
            Debug.Log("Porta: " + serverPort);
            Debug.Log("=================================");

            receiveThread = new Thread(ReceiveLoop);
            receiveThread.IsBackground = true;
            receiveThread.Start();

            SendHello();
        }
        catch (Exception e)
        {
            Debug.LogError(
                "Erro ao iniciar cliente UDP: " + e.Message
            );
        }
    }

    private void SendHello()
    {
        try
        {
            byte[] hello = { 1 };

            udpClient.Send(
                hello,
                hello.Length
            );

            Debug.Log("Pedido de conexão enviado ao servidor.");
        }
        catch (Exception e)
        {
            Debug.LogError(
                "Erro enviando HELLO: " + e.Message
            );
        }
    }

    private void ReceiveLoop()
    {
        IPEndPoint remoteEP =
            new IPEndPoint(
                IPAddress.Any,
                0
            );

        while (!shuttingDown)
        {
            try
            {
                byte[] data =
                    udpClient.Receive(
                        ref remoteEP
                    );

                HandleMessage(data);
            }
            catch (SocketException)
            {
                if (!shuttingDown)
                {
                    Debug.LogWarning(
                        "Conexão UDP encerrada."
                    );
                }

                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception e)
            {
                if (!shuttingDown)
                {
                    Debug.LogError(
                        "Erro recebendo UDP: " + e.Message
                    );
                }

                break;
            }
        }
    }

    private void HandleMessage(byte[] data)
    {
        if (data == null || data.Length == 0)
            return;

        byte msgType = data[0];

        lock (stateLock)
        {
            // --------------------------------
            // WELCOME
            // [2][playerId]
            // --------------------------------
            if (msgType == 2 && data.Length >= 2)
            {
                myPlayerId = data[1];

                connected = true;

                Debug.Log(
                    $"Conectado como Jogador {myPlayerId}"
                );
            }

            // --------------------------------
            // STATE
            // 21 bytes
            // --------------------------------
            else if (msgType == 4 && data.Length >= 21)
            {
                float ballX =
                    BitConverter.ToSingle(
                        data,
                        1
                    );

                float ballY =
                    BitConverter.ToSingle(
                        data,
                        5
                    );

                paddle1Y =
                    BitConverter.ToSingle(
                        data,
                        9
                    );

                paddle2Y =
                    BitConverter.ToSingle(
                        data,
                        13
                    );

                score1 = data[17];

                score2 = data[18];

                gameOver =
                    data[19] == 1;

                winner =
                    data[20];

                ballPos =
                    new Vector2(
                        ballX,
                        ballY
                    );
            }
        }
    }

    private void Update()
    {
        if (!connected)
            return;

        // --------------------------------
        // MOVIMENTO DO JOGADOR
        // --------------------------------

        if (!gameOver)
        {
            float inputY =
                Input.GetAxis("Vertical")
                * 5f
                * Time.deltaTime;

            if (Mathf.Abs(inputY) > 0.0001f)
            {
                SendInput(inputY);
            }
        }

        // --------------------------------
        // APLICA ESTADO RECEBIDO
        // --------------------------------

        lock (stateLock)
        {
            if (ball != null)
            {
                ball.position =
                    new Vector3(
                        ballPos.x,
                        ballPos.y,
                        ball.position.z
                    );
            }

            if (paddle1 != null)
            {
                paddle1.position =
                    new Vector3(
                        paddle1.position.x,
                        paddle1Y,
                        paddle1.position.z
                    );
            }

            if (paddle2 != null)
            {
                paddle2.position =
                    new Vector3(
                        paddle2.position.x,
                        paddle2Y,
                        paddle2.position.z
                    );
            }

            // --------------------------------
            // PLACAR 1
            // --------------------------------

            if (scorePlayer1Text != null)
            {
                scorePlayer1Text.text =
                    score1.ToString();
            }

            // --------------------------------
            // PLACAR 2
            // --------------------------------

            if (scorePlayer2Text != null)
            {
                scorePlayer2Text.text =
                    score2.ToString();
            }

            // --------------------------------
            // IDENTIFICAÇÃO
            // --------------------------------

            if (player1IdText != null)
            {
                player1IdText.text = "JOGADOR 1";
            }

            if (player2IdText != null)
            {
                player2IdText.text = "JOGADOR 2";
            }

            // --------------------------------
            // VITÓRIA
            // --------------------------------

            if (winnerText != null)
            {
                if (gameOver)
                {
                    winnerText.text =
                        $"Jogador {winner} venceu!";
                }
                else
                {
                    winnerText.text = "";
                }
            }
        }
    }

    private void SendInput(float inputY)
    {
        try
        {
            byte[] msg =
                new byte[5];

            msg[0] = 3;

            Buffer.BlockCopy(
                BitConverter.GetBytes(inputY),
                0,
                msg,
                1,
                4
            );

            udpClient.Send(
                msg,
                msg.Length
            );
        }
        catch (Exception e)
        {
            if (!shuttingDown)
            {
                Debug.LogError(
                    "Erro enviando movimento: "
                    + e.Message
                );
            }
        }
    }

    private void OnApplicationQuit()
    {
        Disconnect();
    }

    private void Disconnect()
    {
        shuttingDown = true;
        connected = false;

        if (udpClient != null)
        {
            try
            {
                udpClient.Close();
            }
            catch
            {
                // Ignora erros durante o fechamento.
            }

            udpClient = null;
        }

        if (receiveThread != null &&
            receiveThread.IsAlive)
        {
            receiveThread.Join(100);
        }

        receiveThread = null;

        Debug.Log("Cliente UDP encerrado.");
    }
}