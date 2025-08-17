using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

public class UnityClient : MonoBehaviour
{
    public static UnityClient Instance;

    public GameObject createRoomPanel;
    public GameObject joinRoomPanel;
    public GameObject mainLobbyPanel;

    public InputField roomNameInput;
    public Dropdown isPrivateDropdown;
    public InputField passwordInput;
    public Dropdown maxPlayersDropdown;
    public Button createButton;
    public Button cancelCreateButton;

    public InputField joinRoomNameInput;
    public InputField joinPasswordInput;
    public Button joinButton;
    public Button cancelJoinButton;

    public Button openCreatePopupButton;
    public Button openJoinPopupButton;

    private TcpClient client;
    private StreamReader reader;
    private StreamWriter writer;
    private Thread receiveThread;
    private bool isConnected = false;

    private bool cachedSwap = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        ConnectToServer();
    }

    private void Start()
    {
        createButton.onClick.AddListener(CreateRoom);
        joinButton.onClick.AddListener(JoinRoom);
        isPrivateDropdown.onValueChanged.AddListener(OnPrivacyChanged);
        openCreatePopupButton.onClick.AddListener(() => ShowPanel(createRoomPanel));
        openJoinPopupButton.onClick.AddListener(() => ShowPanel(joinRoomPanel));
        cancelCreateButton.onClick.AddListener(() => ShowPanel(mainLobbyPanel));
        cancelJoinButton.onClick.AddListener(() => ShowPanel(mainLobbyPanel));

        ShowPanel(mainLobbyPanel);
        passwordInput.gameObject.SetActive(false);
    }

    void ConnectToServer()
    {
        try
        {
            client = new TcpClient("127.0.0.1", 7777);
            var stream = client.GetStream();
            reader = new StreamReader(stream, Encoding.UTF8);
            writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

            receiveThread = new Thread(ReceiveLoop);
            receiveThread.Start();

            isConnected = true;
            Debug.Log("서버 연결 성공");
        }
        catch (Exception e)
        {
            Debug.LogError("서버 연결 실패: " + e.Message);
        }
    }

    void ReceiveLoop()
    {
        try
        {
            while (true)
            {
                string line = reader.ReadLine();
                if (line == null) break;

                GameMessage msg = JsonConvert.DeserializeObject<GameMessage>(line);
                if (msg == null) continue;

                Debug.Log($"[수신] {msg.Type} : {msg.Data}");

                switch (msg.Type)
                {
                    case MessageType.StartGame:
                        {
                            var startInfo = JsonConvert.DeserializeObject<StartGameInfo>(msg.Data);
                            UnityMainThreadDispatcher.Instance().Enqueue(() =>
                            {
                                SceneManager.LoadScene("GameScene");
                                StartCoroutine(WaitThenRequestMyOrder(startInfo.roomName, startInfo.swap));
                            });
                            break;
                        }

                    case MessageType.MyOrder:
                        {
                            int index = int.Parse(msg.Data);
                            UnityMainThreadDispatcher.Instance().Enqueue(() =>
                            {
                                GameManager.Instance.SpawnPlayers(index, cachedSwap);
                            });
                            break;
                        }

                    case MessageType.Move:
                        {
                            var moveData = JsonConvert.DeserializeObject<MoveData>(msg.Data);
                            UnityMainThreadDispatcher.Instance().Enqueue(() =>
                            {
                                GameManager.Instance.UpdateOtherPlayerPosition(moveData.x, moveData.y);
                            });
                            break;
                        }

                    case MessageType.ItemSpawn:
                        {
                            var spawn = JsonConvert.DeserializeObject<ItemSpawnDTO>(msg.Data);
                            UnityMainThreadDispatcher.Instance().Enqueue(() =>
                            {
                                GameManager.Instance.SpawnItemFromServer(spawn.itemId, spawn.instanceId, spawn.x, spawn.y);
                            });
                            break;
                        }

                    case MessageType.ItemRemove:
                        {
                            string instanceId = msg.Data;
                            UnityMainThreadDispatcher.Instance().Enqueue(() =>
                            {
                                GameManager.Instance.RemoveItem(instanceId);
                            });
                            break;
                        }

                    case MessageType.ApplyBuff:
                        {
                            var buff = JsonConvert.DeserializeObject<BuffDTO>(msg.Data);
                            UnityMainThreadDispatcher.Instance().Enqueue(() =>
                            {
                                GameManager.Instance.ApplyBuffToMe(buff.type, buff.value, buff.duration);
                            });
                            break;
                        }

                    case MessageType.ScoreUpdate:
                        {
                            var s = JsonConvert.DeserializeObject<ScoreDTO>(msg.Data);
                            UnityMainThreadDispatcher.Instance().Enqueue(() =>
                            {
                                GameManager.Instance.UpdateScore(s.who, s.add, s.score);
                            });
                            break;
                        }

                    case MessageType.BagUpdate:
                        {
                            var b = JsonConvert.DeserializeObject<BagDTO>(msg.Data);
                            UnityMainThreadDispatcher.Instance().Enqueue(() =>
                            {
                                GameManager.Instance.OnBagUpdate(b.bag);
                            });
                            break;
                        }

                    case MessageType.Error:
                        {
                            Debug.LogWarning("[서버 오류] " + msg.Data);
                            break;
                        }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ReceiveLoop 예외] {ex.Message}");
        }
    }

    private System.Collections.IEnumerator WaitThenRequestMyOrder(string roomName, bool swap)
    {
        cachedSwap = swap;
        yield return new WaitForSeconds(0.5f);
        SendToServer(new GameMessage
        {
            Type = MessageType.MyOrder,
            Data = roomName
        });
    }

    void SendToServer(GameMessage msg)
    {
        try
        {
            string json = JsonConvert.SerializeObject(msg);
            writer.WriteLine(json);
            Debug.Log($"[전송] {msg.Type} : {msg.Data}");
        }
        catch (Exception e)
        {
            Debug.LogError("서버 전송 실패: " + e.Message);
        }
    }

    public void SendMove(float x, float y)
    {
        var move = new MoveData { x = x, y = y };
        SendToServer(new GameMessage
        {
            Type = MessageType.Move,
            Data = JsonConvert.SerializeObject(move)
        });
    }

    public void SendItemPickup(string instanceId)
    {
        SendToServer(new GameMessage
        {
            Type = MessageType.ItemPickup,
            Data = instanceId
        });
    }

    public void SendDepositBag()
    {
        SendToServer(new GameMessage
        {
            Type = MessageType.DepositBag,
            Data = ""
        });
    }

    void CreateRoom()
    {
        if (!isConnected) return;

        var room = new RoomData
        {
            Name = roomNameInput.text,
            IsPrivate = isPrivateDropdown.value == 1,
            Password = passwordInput.text,
            MaxPlayers = int.Parse(maxPlayersDropdown.options[maxPlayersDropdown.value].text)
        };

        SendToServer(new GameMessage
        {
            Type = MessageType.CreateRoom,
            Data = JsonConvert.SerializeObject(room)
        });
    }

    void JoinRoom()
    {
        if (!isConnected) return;

        var joinData = new JoinRoomRequest
        {
            roomName = joinRoomNameInput.text,
            password = joinPasswordInput.text
        };

        SendToServer(new GameMessage
        {
            Type = MessageType.JoinRoom,
            Data = JsonConvert.SerializeObject(joinData)
        });
    }

    void ShowPanel(GameObject panel)
    {
        createRoomPanel.SetActive(false);
        joinRoomPanel.SetActive(false);
        mainLobbyPanel.SetActive(false);
        panel.SetActive(true);
    }

    void OnPrivacyChanged(int index)
    {
        passwordInput.gameObject.SetActive(index == 1);
    }

    private void OnApplicationQuit()
    {
        try { receiveThread?.Abort(); } catch { }
        try { writer?.Close(); } catch { }
        try { reader?.Close(); } catch { }
        try { client?.Close(); } catch { }
    }

    [Serializable]
    public class RoomData
    {
        public string Name;
        public bool IsPrivate;
        public string Password;
        public int MaxPlayers;
    }

    [Serializable]
    public class JoinRoomRequest
    {
        public string roomName;
        public string password;
    }

    [Serializable]
    public class GameMessage
    {
        public MessageType Type;
        public string Data;
    }

    [Serializable] public class MoveData { public float x; public float y; }
    [Serializable] public class StartGameInfo { public string roomName; public bool swap; }
    [Serializable] public class ItemSpawnDTO { public string instanceId; public int itemId; public float x; public float y; }
    [Serializable] public class BuffDTO { public string type; public float value; public float duration; }

    [Serializable] public class ScoreDTO { public int who; public int score; public int add; }
    [Serializable] public class BagDTO { public int bag; }

    public enum MessageType
    {
        CreateRoom,
        JoinRoom,
        StartGame,
        RoomList,
        Error,
        MyOrder,
        Move,
        ItemSpawn,
        ItemPickup,
        ItemRemove,
        ApplyBuff,
        ScoreUpdate,
        BagUpdate,
        DepositBag
    }
}
