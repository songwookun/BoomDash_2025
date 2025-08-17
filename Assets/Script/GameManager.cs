using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Prefabs")]
    public GameObject player1Prefab;
    public GameObject player2Prefab;
    public GameObject areaPrefab;
    public GameObject area2Prefab;

    public GameObject goldPrefab;
    public GameObject speedBoostPrefab;

    [Header("TopBar UI")]
    public Text bagCountText;
    public Text scoreText;
    public Text otherScoreText;
    public Text timerText;

    [Header("End Popup")]
    public GameObject endPopup;
    public Text endTitleText;
    public Button rematchButton;
    public Button exitButton;
    public Text waitingText;

    // --- runtime ---
    private GameObject myPlayer;
    private GameObject otherPlayer;

    private GameObject areaObjRef;
    private GameObject area2ObjRef;
    private Transform areaPos;
    private Transform area2Pos;

    private Dictionary<string, GameObject> spawnedItems = new Dictionary<string, GameObject>();
    private Dictionary<string, int> instanceToItemId = new Dictionary<string, int>();

    private const int bagCapacity = 5;
    private int bagCount = 0;

    private int myScore = 0;
    private int otherScore = 0;

    private int myIndex = -1;

    private Bounds myAreaBounds;
    private Bounds otherAreaBounds;
    private bool myAreaReady = false;

    private bool mineInAreaA = true;

    [SerializeField] private float marginX = 1.2f;
    [SerializeField] private float marginY = 1.2f;
    [SerializeField] private float area2ExtraDownY = 0.6f;

    private float depositCooldown = 0.2f;
    private float lastDepositSent = -1f;

    private float _lastOrthoSize = -1f;
    private int _lastW = -1, _lastH = -1;

    private bool matchEnded = false;
    private int timeLeftSec = 300; 

    void Awake()
    {
        Instance = this;
        PlayerStatLoader.LoadStats();
        RefreshUI();

        if (endPopup) endPopup.SetActive(false);
        if (waitingText) waitingText.gameObject.SetActive(false);
        if (rematchButton) rematchButton.onClick.AddListener(OnClickRematch);
        if (exitButton) exitButton.onClick.AddListener(OnClickExit);
    }

    public void SpawnPlayers(int myIndex, bool swap)
    {
        this.myIndex = myIndex;
        mineInAreaA = ((myIndex == 0) ^ swap);

        Vector3 lb, rt;
        GetCornerPositions(out lb, out rt);

        areaObjRef = Instantiate(areaPrefab, lb, Quaternion.identity);
        area2ObjRef = Instantiate(area2Prefab, rt, Quaternion.identity);

        areaPos = areaObjRef.transform;
        area2Pos = area2ObjRef.transform;

        GameObject myPrefab = myIndex == 0 ? player1Prefab : player2Prefab;
        GameObject otherPrefab = myIndex == 0 ? player2Prefab : player1Prefab;

        Vector3 mySpawn = GetInnerSpawn(mineInAreaA ? areaPos : area2Pos);
        Vector3 otherSpawn = GetInnerSpawn(mineInAreaA ? area2Pos : areaPos);

        myPlayer = Instantiate(myPrefab, mySpawn, Quaternion.identity);
        myPlayer.GetComponent<PlayerController>().isMine = true;

        otherPlayer = Instantiate(otherPrefab, otherSpawn, Quaternion.identity);
        otherPlayer.GetComponent<PlayerController>().isMine = false;

        RebuildAreaBoundsAndForbidden();

        myScore = 0;
        otherScore = 0;
        bagCount = 0;
        RefreshUI();

        matchEnded = false;
        timeLeftSec = 300;
        UpdateTimerUI();

        CacheScreenCameraState();
    }

    private void GetCornerPositions(out Vector3 lb, out Vector3 rt)
    {
        var cam = Camera.main;
        if (!cam)
        {
            lb = new Vector3(-8, -4, 0);
            rt = new Vector3(8, 4 - area2ExtraDownY, 0);
            return;
        }

        float z = -cam.transform.position.z;
        Vector3 bl = cam.ViewportToWorldPoint(new Vector3(0f, 0f, z));
        Vector3 tr = cam.ViewportToWorldPoint(new Vector3(1f, 1f, z));

        lb = new Vector3(bl.x + marginX, bl.y + marginY, 0f);
        rt = new Vector3(tr.x - marginX, tr.y - marginY - area2ExtraDownY, 0f);
    }

    private Vector3 GetInnerSpawn(Transform areaTransform)
    {
        float radius = 0.3f;
        float offsetX = Random.Range(-radius, radius);
        float offsetY = Random.Range(-radius, radius);
        return areaTransform.position + new Vector3(offsetX, offsetY, 0);
    }

    private Bounds GetBounds(GameObject go)
    {
        var col = go.GetComponentInChildren<Collider2D>();
        if (col != null) return col.bounds;
        var sr = go.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) return sr.bounds;
        return new Bounds(go.transform.position, new Vector3(2f, 2f, 0f));
    }

    private void RebuildAreaBoundsAndForbidden()
    {
        if (areaObjRef == null || area2ObjRef == null || myPlayer == null || otherPlayer == null) return;

        Bounds areaA = GetBounds(areaObjRef);
        Bounds areaB = GetBounds(area2ObjRef);

        myAreaBounds = mineInAreaA ? areaA : areaB;
        otherAreaBounds = mineInAreaA ? areaB : areaA;
        myAreaReady = true;

        var myPC = myPlayer.GetComponent<PlayerController>();
        var otherPC = otherPlayer.GetComponent<PlayerController>();
        if (myPC) myPC.SetForbiddenBounds(otherAreaBounds);
        if (otherPC) otherPC.SetForbiddenBounds(myAreaBounds);
    }

    private void RepositionAreasToCorners()
    {
        if (areaPos == null || area2Pos == null) return;

        Vector3 lb, rt;
        GetCornerPositions(out lb, out rt);
        areaPos.position = lb;
        area2Pos.position = rt;

        RebuildAreaBoundsAndForbidden();
    }

    private bool CameraOrScreenChanged()
    {
        var cam = Camera.main;
        if (!cam) return false;
        return Mathf.Abs(_lastOrthoSize - cam.orthographicSize) > 0.01f ||
               _lastW != Screen.width || _lastH != Screen.height;
    }

    private void CacheScreenCameraState()
    {
        var cam = Camera.main;
        if (!cam) return;
        _lastOrthoSize = cam.orthographicSize;
        _lastW = Screen.width;
        _lastH = Screen.height;
    }

    public void SpawnItemFromServer(int itemId, string instanceId, float x, float y)
    {
        if (spawnedItems.ContainsKey(instanceId)) return;

        GameObject prefab = null;
        switch (itemId)
        {
            case 10000: prefab = goldPrefab; break;
            case 11000: prefab = speedBoostPrefab; break;
            default:
                Debug.LogWarning($"알 수 없는 아이템 ID: {itemId}");
                return;
        }

        var go = Instantiate(prefab, new Vector3(x, y, 0), Quaternion.identity);
        var pick = go.GetComponent<ItemPickupBehavior>();
        if (pick == null) pick = go.AddComponent<ItemPickupBehavior>();
        pick.Initialize(instanceId);

        spawnedItems[instanceId] = go;
        instanceToItemId[instanceId] = itemId;
    }

    public void RemoveItem(string instanceId)
    {
        if (spawnedItems.TryGetValue(instanceId, out var go))
        {
            Destroy(go);
            spawnedItems.Remove(instanceId);
        }
        instanceToItemId.Remove(instanceId);
    }

    public bool TryPickupLocally(string instanceId)
    {
        if (!instanceToItemId.TryGetValue(instanceId, out int itemId))
            return false;

        if (itemId == 10000)
        {
            if (bagCount >= bagCapacity) return false;
            return true;
        }
        return true;
    }

    public void UpdateOtherPlayerPosition(float x, float y)
    {
        if (otherPlayer != null)
            otherPlayer.GetComponent<PlayerController>().SetPosition(new Vector3(x, y, 0));
    }

    public void OnBagUpdate(int bag)
    {
        bagCount = bag;
        RefreshUI();
    }

    public void UpdateScore(int who, int add, int total)
    {
        if (who == myIndex) myScore = total;
        else otherScore = total;
        RefreshUI();
    }

    public void UpdateMyScore(int add, int total)
    {
        myScore = total;
        RefreshUI();
    }

    public void ApplyBuffToMe(string type, float value, float duration)
    {
        if (myPlayer == null) return;
        var pc = myPlayer.GetComponent<PlayerController>();
        if (!pc) return;
        if (type == "PlayerMoveSpeedUp") pc.ApplySpeedBuff(value, duration);
    }

    private void UpdateTimerUI()
    {
        if (!timerText) return;
        int m = timeLeftSec / 60;
        int s = timeLeftSec % 60;
        timerText.text = $"{m:00}:{s:00}";
    }

    public void UpdateTimerFromServer(int sec)
    {
        timeLeftSec = Mathf.Max(0, sec);
        UpdateTimerUI();
    }

    public void OnMatchOver(int winnerIndex, int p0Score, int p1Score)
    {
        matchEnded = true;

        if (myIndex == 0) { myScore = p0Score; otherScore = p1Score; }
        else { myScore = p1Score; otherScore = p0Score; }
        RefreshUI();

        string title = "무승부";
        if (winnerIndex == myIndex) title = "승리!";
        else if (winnerIndex >= 0) title = "패배";

        if (endTitleText) endTitleText.text = title;
        if (waitingText) waitingText.gameObject.SetActive(false);
        if (endPopup) endPopup.SetActive(true);
        if (rematchButton) rematchButton.interactable = true;
        if (exitButton) exitButton.interactable = true;
    }

    void Update()
    {
        if (!myAreaReady || myPlayer == null) return;

        if (CameraOrScreenChanged())
        {
            RepositionAreasToCorners();
            CacheScreenCameraState();
        }

        if (!matchEnded && bagCount > 0 && Inside(myAreaBounds, myPlayer.transform.position))
        {
            if (Time.time - lastDepositSent > depositCooldown)
            {
                UnityClient.Instance.SendDepositBag();
                lastDepositSent = Time.time;
            }
        }
    }

    private bool Inside(Bounds b, Vector3 p)
    {
        const float EPS = 0.01f;
        return (p.x > b.min.x + EPS && p.x < b.max.x - EPS &&
                p.y > b.min.y + EPS && p.y < b.max.y - EPS);
    }

    private void RefreshUI()
    {
        if (bagCountText) bagCountText.text = $"{bagCount}/{bagCapacity}";
        if (scoreText) scoreText.text = myScore.ToString();
        if (otherScoreText) otherScoreText.text = otherScore.ToString();
    }

    private void OnClickRematch()
    {
        if (rematchButton) rematchButton.interactable = false;
        if (exitButton) exitButton.interactable = false;
        if (waitingText) { waitingText.text = "상대 대기 중…"; waitingText.gameObject.SetActive(true); }

        UnityClient.Instance.RequestRematch();
    }

    private void OnClickExit()
    {
        if (rematchButton) rematchButton.interactable = false;
        if (exitButton) exitButton.interactable = false;
        if (waitingText) { waitingText.text = "나가기…"; waitingText.gameObject.SetActive(true); }

        UnityClient.Instance.RequestExitToLobby();
    }
}
