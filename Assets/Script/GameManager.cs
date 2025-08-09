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

    private GameObject myPlayer;
    private GameObject otherPlayer;

    private Transform areaPos;
    private Transform area2Pos;

    private Dictionary<string, GameObject> spawnedItems = new Dictionary<string, GameObject>();
    private Dictionary<string, int> instanceToItemId = new Dictionary<string, int>();

    private const int bagCapacity = 5;
    private int bagCount = 0;
    private int localScore = 0;

    private Bounds myAreaBounds;
    private Bounds otherAreaBounds;
    private bool myAreaReady = false;

    private float depositCooldown = 0.2f;
    private float lastDepositSent = -1f;

    void Awake()
    {
        Instance = this;
        PlayerStatLoader.LoadStats();
        RefreshUI();
    }

    public void SpawnPlayers(int myIndex, bool swap)
    {
        Debug.Log($"[GameManager] 플레이어 스폰 - 내 인덱스: {myIndex}, swap: {swap}");

        Vector3 areaSpawnPos_LB = new Vector3(-8, -4, 0);
        Vector3 areaSpawnPos_RT = new Vector3(8, 4, 0);

        GameObject areaObj = Instantiate(areaPrefab, areaSpawnPos_LB, Quaternion.identity);
        GameObject area2Obj = Instantiate(area2Prefab, areaSpawnPos_RT, Quaternion.identity);

        areaPos = areaObj.transform;
        area2Pos = area2Obj.transform;

        Vector3 GetInnerSpawn(Transform areaTransform)
        {
            float radius = 0.3f;
            float offsetX = Random.Range(-radius, radius);
            float offsetY = Random.Range(-radius, radius);
            return areaTransform.position + new Vector3(offsetX, offsetY, 0);
        }

        GameObject myPrefab = myIndex == 0 ? player1Prefab : player2Prefab;
        GameObject otherPrefab = myIndex == 0 ? player2Prefab : player1Prefab;

        bool mineInAreaA = ((myIndex == 0) ^ swap);

        Vector3 mySpawn = mineInAreaA ? GetInnerSpawn(areaPos) : GetInnerSpawn(area2Pos);
        Vector3 otherSpawn = mineInAreaA ? GetInnerSpawn(area2Pos) : GetInnerSpawn(areaPos);

        myPlayer = Instantiate(myPrefab, mySpawn, Quaternion.identity);
        myPlayer.GetComponent<PlayerController>().isMine = true;

        otherPlayer = Instantiate(otherPrefab, otherSpawn, Quaternion.identity);
        otherPlayer.GetComponent<PlayerController>().isMine = false;

        Bounds GetBounds(GameObject go)
        {
            var col = go.GetComponentInChildren<Collider2D>();
            if (col != null) return col.bounds;
            var sr = go.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) return sr.bounds;
            return new Bounds(go.transform.position, new Vector3(2f, 2f, 0f));
        }

        Bounds areaA = GetBounds(areaObj);
        Bounds areaB = GetBounds(area2Obj);

        myAreaBounds = mineInAreaA ? areaA : areaB;
        otherAreaBounds = mineInAreaA ? areaB : areaA;
        myAreaReady = true;

        myPlayer.GetComponent<PlayerController>().SetForbiddenBounds(otherAreaBounds);
        otherPlayer.GetComponent<PlayerController>().SetForbiddenBounds(myAreaBounds);

        RefreshUI();
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

        if (bagCount > 0 && Inside(myAreaBounds, myPlayer.transform.position))
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
        if (bagCountText != null) bagCountText.text = $"{bagCount}/{bagCapacity}";
        if (scoreText != null) scoreText.text = $"{localScore}";
    }

    public void OnBagUpdate(int bag)
    {
        bagCount = bag;
        RefreshUI();
    }

    public void UpdateMyScore(int add, int total)
    {
        localScore = total;
        Debug.Log($"[ServerScore] +{add} → 총점 {total}");
        RefreshUI();
    }

    public void ApplyBuffToMe(string type, float value, float duration)
    {
        if (myPlayer == null) return;
        var pc = myPlayer.GetComponent<PlayerController>();
        if (pc == null) return;
        if (type == "PlayerMoveSpeedUp") pc.ApplySpeedBuff(value, duration);
    }
}
