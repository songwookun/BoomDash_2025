using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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

    [Header("Canvas UI")]
    public Text bagCountText;     
    public Text scoreText;        
    public Text otherScoreText;   

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

    void Awake()
    {
        Instance = this;
        PlayerStatLoader.LoadStats();
        RefreshUI();
    }

    public void SpawnPlayers(int myIndex, bool swap)
    {
        this.myIndex = myIndex;
        Debug.Log($"[GameManager] 플레이어 스폰 - 내 인덱스: {myIndex}, swap: {swap}");

        Vector3 areaSpawnPos_LB, areaSpawnPos_RT;
        GetCornerPositions(out areaSpawnPos_LB, out areaSpawnPos_RT);

        areaObjRef = Instantiate(areaPrefab, areaSpawnPos_LB, Quaternion.identity);
        area2ObjRef = Instantiate(area2Prefab, areaSpawnPos_RT, Quaternion.identity);

        areaPos = areaObjRef.transform;
        area2Pos = area2ObjRef.transform;

        GameObject myPrefab = myIndex == 0 ? player1Prefab : player2Prefab;
        GameObject otherPrefab = myIndex == 0 ? player2Prefab : player1Prefab;

        mineInAreaA = ((myIndex == 0) ^ swap);

        Vector3 mySpawn = GetInnerSpawn(mineInAreaA ? areaPos : area2Pos);
        Vector3 otherSpawn = GetInnerSpawn(mineInAreaA ? area2Pos : areaPos);

        myPlayer = Instantiate(myPrefab, mySpawn, Quaternion.identity);
        myPlayer.GetComponent<PlayerController>().isMine = true;

        otherPlayer = Instantiate(otherPrefab, otherSpawn, Quaternion.identity);
        otherPlayer.GetComponent<PlayerController>().isMine = false;

        RebuildAreaBoundsAndForbidden();

        myScore = 0;
        otherScore = 0;
        RefreshUI();

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

    private void RepositionAreasToCorners()
    {
        if (areaPos == null || area2Pos == null) return;

        Vector3 lb, rt;
        GetCornerPositions(out lb, out rt);
        areaPos.position = lb;
        area2Pos.position = rt;

        RebuildAreaBoundsAndForbidden();
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

    public void UpdateOtherPlayerPosition(float x, float y)
    {
        if (otherPlayer != null)
            otherPlayer.GetComponent<PlayerController>().SetPosition(new Vector3(x, y, 0));
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
            if (bagCount >= bagCapacity)
            {
                Debug.Log("[Bag] 가방이 가득 찼습니다. (서버 기준 동기화)");
                return false;
            }
            return true;
        }
        return true;
    }

    void Update()
    {
        if (!myAreaReady || myPlayer == null) return;

        if (CameraOrScreenChanged())
        {
            RepositionAreasToCorners();
            CacheScreenCameraState();
        }

        if (bagCount > 0 && Inside(myAreaBounds, myPlayer.transform.position))
        {
            if (Time.time - lastDepositSent > depositCooldown)
            {
                UnityClient.Instance.SendDepositBag();
                lastDepositSent = Time.time;
            }
        }
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

    private bool Inside(Bounds b, Vector3 p)
    {
        const float EPS = 0.01f;
        return (p.x > b.min.x + EPS && p.x < b.max.x - EPS &&
                p.y > b.min.y + EPS && p.y < b.max.y - EPS);
    }

    private void RefreshUI()
    {
        if (bagCountText != null) bagCountText.text = $"{bagCount}/{bagCapacity}";
        if (scoreText != null) scoreText.text = myScore.ToString();
        if (otherScoreText != null) otherScoreText.text = otherScore.ToString();
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
        Debug.Log($"[ScoreSync] who={who}, +{add} → total={total} / (me:{myScore}, other:{otherScore})");
    }
    public void UpdateMyScore(int add, int total)
    {
        myScore = total;
        RefreshUI();
        Debug.Log($"[ServerScore-Compat] +{add} → 총점 {total}");
    }

    public void ApplyBuffToMe(string type, float value, float duration)
    {
        if (myPlayer == null) return;
        var pc = myPlayer.GetComponent<PlayerController>();
        if (pc == null) return;
        if (type == "PlayerMoveSpeedUp") pc.ApplySpeedBuff(value, duration);
    }
}
