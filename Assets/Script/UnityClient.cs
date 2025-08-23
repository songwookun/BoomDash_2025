using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections;
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
    private bool pendingReconnectOnLobby = false; 

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;
        ConnectToServer();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnApplicationQuit()
    {
        try { receiveThread?.Abort(); } catch { }
        try { writer?.Close(); } catch { }
        try { reader?.Close(); } catch { }
        try { client?.Close(); } catch { }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "MainScene")
        {
            LobbyUIBinder binder = null;
#if UNITY_2023_1_OR_NEWER
            binder = UnityEngine.Object.FindFirstObjectByType<LobbyUIBinder>(FindObjectsInactive.Include);
            if (binder == null)
                binder = UnityEngine.Object.FindAnyObjectByType<LobbyUIBinder>();
#else
        binder = UnityEngine.Object.FindObjectOfType<LobbyUIBinder>();
#endif
            if (binder != null) BindLobbyUI(binder);

            if (pendingReconnectOnLobby)
            {
                pendingReconnectOnLobby = false;
                SafeReconnect();
            }
        }
    }

    public void BindLobbyUI(LobbyUIBinder b)
    {
        createRoomPanel = b.createRoomPanel;
        joinRoomPanel = b.joinRoomPanel;
        mainLobbyPanel = b.mainLobbyPanel;

        roomNameInput = b.roomNameInput;
        isPrivateDropdown = b.isPrivateDropdown;
        passwordInput = b.passwordInput;
        maxPlayersDropdown = b.maxPlayersDropdown;
        createButton = b.createButton;
        cancelCreateButton = b.cancelCreateButton;

        joinRoomNameInput = b.joinRoomNameInput;
        joinPasswordInput = b.joinPasswordInput;
        joinButton = b.joinButton;
        cancelJoinButton = b.cancelJoinButton;

        openCreatePopupButton = b.openCreatePopupButton;
        openJoinPopupButton = b.openJoinPopupButton;

        if (createButton)
        {
            createButton.onClick.RemoveAllListeners();
            createButton.onClick.AddListener(CreateRoom);
        }
        if (joinButton)
        {
            joinButton.onClick.RemoveAllListeners();
            joinButton.onClick.AddListener(JoinRoom);     
        }
        if (isPrivateDropdown)
        {
            isPrivateDropdown.onValueChanged.RemoveAllListeners();
            isPrivateDropdown.onValueChanged.AddListener(OnPrivacyChanged);
        }
        if (openCreatePopupButton)
        {
            openCreatePopupButton.onClick.RemoveAllListeners();
            openCreatePopupButton.onClick.AddListener(() => ShowPanel(createRoomPanel));
        }
        if (openJoinPopupButton)
        {
            openJoinPopupButton.onClick.RemoveAllListeners();
            openJoinPopupButton.onClick.AddListener(() => ShowPanel(joinRoomPanel));
        }
        if (cancelCreateButton)
        {
            cancelCreateButton.onClick.RemoveAllListeners();
            cancelCreateButton.onClick.AddListener(() => ShowPanel(mainLobbyPanel));
        }
        if (cancelJoinButton)
        {
            cancelJoinButton.onClick.RemoveAllListeners();
            cancelJoinButton.onClick.AddListener(() => ShowPanel(mainLobbyPanel));
        }

        if (passwordInput) passwordInput.gameObject.SetActive(isPrivateDropdown && isPrivateDropdown.value == 1);
        ShowPanel(mainLobbyPanel);
    }

    private void ShowPanel(GameObject panel)
    {
        if (createRoomPanel) TogglePanel(createRoomPanel, false);
        if (joinRoomPanel) TogglePanel(joinRoomPanel, false);
        if (mainLobbyPanel) TogglePanel(mainLobbyPanel, false);
        if (panel) TogglePanel(panel, true);
    }

    private void TogglePanel(GameObject go, bool on)
    {
        go.SetActive(on);
        var cg = go.GetComponent<CanvasGroup>();
        if (!cg) cg = go.AddComponent<CanvasGroup>();
        cg.alpha = on ? 1f : 0f;
        cg.interactable = on;
        cg.blocksRaycasts = on; 
    }

    private void OnPrivacyChanged(int index)
    {
        if (passwordInput) passwordInput.gameObject.SetActive(index == 1);
    }

    private void ConnectToServer()
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
            isConnected = false;
            Debug.LogError("서버 연결 실패: " + e.Message);
        }
    }

    private void SafeReconnect()
    {
        try { receiveThread?.Abort(); } catch { }
        try { writer?.Close(); } catch { }
        try { reader?.Close(); } catch { }
        try { client?.Close(); } catch { }
        isConnected = false;

        ConnectToServer();
    }

    private void ReceiveLoop()
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

                    case MessageType.TimerSync:
                        {
                            int sec = int.Parse(msg.Data);
                            UnityMainThreadDispatcher.Instance().Enqueue(() =>
                            {
                                GameManager.Instance.UpdateTimerFromServer(sec);
                            });
                            break;
                        }

                    case MessageType.MatchOver:
                        {
                            var over = JsonConvert.DeserializeObject<MatchOverDTO>(msg.Data);
                            UnityMainThreadDispatcher.Instance().Enqueue(() =>
                            {
                                GameManager.Instance.OnMatchOver(over.winner, over.p0, over.p1);
                            });
                            break;
                        }

                    case MessageType.ReturnToLobby:
                        {
                            UnityMainThreadDispatcher.Instance().Enqueue(() =>
                            {
                                pendingReconnectOnLobby = true;       // 로비에서 재접속 예약
                                SceneManager.LoadScene("MainScene");  // 로비로 이동
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

    private IEnumerator WaitThenRequestMyOrder(string roomName, bool swap)
    {
        cachedSwap = swap;
        yield return new WaitForSeconds(0.5f);
        SendToServer(new GameMessage
        {
            Type = MessageType.MyOrder,
            Data = roomName
        });
    }

    private void SendToServer(GameMessage msg)
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
        SendToServer(new GameMessage { Type = MessageType.ItemPickup, Data = instanceId });
    }

    public void SendDepositBag()
    {
        SendToServer(new GameMessage { Type = MessageType.DepositBag, Data = "" });
    }

    public void RequestRematch()
    {
        SendToServer(new GameMessage { Type = MessageType.RequestRematch, Data = "" });
    }

    public void RequestExitToLobby()
    {
        SendToServer(new GameMessage { Type = MessageType.ExitToLobby, Data = "" });
    }

    // === 버튼 핸들러(누락되었던 부분 추가) ===
    private void CreateRoom()
    {
        if (!isConnected)
        {
            Debug.LogWarning("서버 미연결 상태: CreateRoom 무시");
            return;
        }

        var room = new RoomData
        {
            Name = roomNameInput ? roomNameInput.text : "Room",
            IsPrivate = isPrivateDropdown ? isPrivateDropdown.value == 1 : false,
            Password = passwordInput ? passwordInput.text : "",
            MaxPlayers = maxPlayersDropdown ? int.Parse(maxPlayersDropdown.options[maxPlayersDropdown.value].text) : 2
        };

        SendToServer(new GameMessage
        {
            Type = MessageType.CreateRoom,
            Data = JsonConvert.SerializeObject(room)
        });
    }

    private void JoinRoom()
    {
        if (!isConnected)
        {
            Debug.LogWarning("서버 미연결 상태: JoinRoom 무시");
            return;
        }

        var joinData = new JoinRoomRequest
        {
            roomName = joinRoomNameInput ? joinRoomNameInput.text : "",
            password = joinPasswordInput ? joinPasswordInput.text : ""
        };

        SendToServer(new GameMessage
        {
            Type = MessageType.JoinRoom,
            Data = JsonConvert.SerializeObject(joinData)
        });
    }

    [Serializable] public class JoinRoomRequest { public string roomName; public string password; }

    [Serializable]
    public class RoomData
    {
        public string Name;
        public bool IsPrivate;
        public string Password;
        public int MaxPlayers;
    }

    [Serializable] public class GameMessage { public MessageType Type; public string Data; }
    [Serializable] public class MoveData { public float x; public float y; }
    [Serializable] public class StartGameInfo { public string roomName; public bool swap; }
    [Serializable] public class ItemSpawnDTO { public string instanceId; public int itemId; public float x; public float y; }
    [Serializable] public class BuffDTO { public string type; public float value; public float duration; }
    [Serializable] public class ScoreDTO { public int who; public int score; public int add; }
    [Serializable] public class BagDTO { public int bag; }
    [Serializable] public class MatchOverDTO { public int winner; public int p0; public int p1; }

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
        DepositBag,
        RequestRematch,
        ExitToLobby,
        ReturnToLobby,
        TimerSync,
        MatchOver
    }
}
