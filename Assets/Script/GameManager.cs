using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public GameObject player1Prefab;
    public GameObject player2Prefab;
    public GameObject areaPrefab;
    public GameObject area2Prefab;

    public GameObject goldPrefab;
    public GameObject speedBoostPrefab;

    private GameObject myPlayer;
    private GameObject otherPlayer;

    private Transform areaPos;
    private Transform area2Pos;

    private Dictionary<string, GameObject> spawnedItems = new Dictionary<string, GameObject>();

    void Awake()
    {
        Instance = this;
        PlayerStatLoader.LoadStats();
    }

    public void SpawnPlayers(int myIndex, bool swap)
    {
        Debug.Log($"[GameManager] 플레이어 스폰 - 내 인덱스: {myIndex}, swap: {swap}");

        Vector3 areaSpawnPos_LB = new Vector3(-8, -4, 0);
        Vector3 areaSpawnPos_RT = new Vector3(8, 4, 0);

        GameObject areaObj = Instantiate(areaPrefab, swap ? areaSpawnPos_LB : areaSpawnPos_RT, Quaternion.identity);
        GameObject area2Obj = Instantiate(area2Prefab, swap ? areaSpawnPos_RT : areaSpawnPos_LB, Quaternion.identity);

        areaPos = areaObj.transform;
        area2Pos = area2Obj.transform;

        Vector3 GetInnerSpawn(Transform areaTransform)
        {
            float radius = 0.3f;
            float offsetX = Random.Range(-radius, radius);
            float offsetY = Random.Range(-radius, radius);
            return areaTransform.position + new Vector3(offsetX, offsetY, 0);
        }

        int otherIndex = 1 - myIndex;
        GameObject myPrefab = myIndex == 0 ? player1Prefab : player2Prefab;
        GameObject otherPrefab = myIndex == 0 ? player2Prefab : player1Prefab;

        Vector3 mySpawn = myIndex == 0 ? GetInnerSpawn(areaPos) : GetInnerSpawn(area2Pos);
        Vector3 otherSpawn = myIndex == 0 ? GetInnerSpawn(area2Pos) : GetInnerSpawn(areaPos);

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

        Bounds myAreaBounds = (myIndex == 0) ? areaA : areaB;
        Bounds otherAreaBounds = (myIndex == 0) ? areaB : areaA;

        myPlayer.GetComponent<PlayerController>().SetForbiddenBounds(otherAreaBounds);
        otherPlayer.GetComponent<PlayerController>().SetForbiddenBounds(myAreaBounds);
    }

    public void UpdateOtherPlayerPosition(float x, float y)
    {
        if (otherPlayer != null)
        {
            otherPlayer.GetComponent<PlayerController>().SetPosition(new Vector3(x, y, 0));
        }
    }

    public void SpawnItemFromServer(int itemId, string instanceId, float x, float y)
    {
        if (spawnedItems.ContainsKey(instanceId)) return;

        GameObject prefab = null;
        switch (itemId)
        {
            case 10000: prefab = goldPrefab; break;
            case 11000: prefab = speedBoostPrefab; break;
            default: Debug.LogWarning($"알 수 없는 아이템 ID: {itemId}"); return;
        }

        var go = Instantiate(prefab, new Vector3(x, y, 0), Quaternion.identity);
        var pick = go.GetComponent<ItemPickupBehavior>();
        if (pick == null) pick = go.AddComponent<ItemPickupBehavior>();
        pick.Initialize(instanceId);

        spawnedItems[instanceId] = go;
    }

    public void RemoveItem(string instanceId)
    {
        if (spawnedItems.TryGetValue(instanceId, out var go))
        {
            Destroy(go);
            spawnedItems.Remove(instanceId);
        }
    }
}
