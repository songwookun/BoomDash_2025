using UnityEngine;

public class ItemPickupBehavior : MonoBehaviour
{
    [SerializeField] private string instanceId;
    private Collider2D col;
    private SpriteRenderer sr;

    public void Initialize(string id)
    {
        instanceId = id;
        col = GetComponent<Collider2D>();
        if (col == null)
        {
            col = gameObject.AddComponent<CircleCollider2D>();
            ((CircleCollider2D)col).isTrigger = true;
        }
        sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
        col.enabled = true;
        if (sr != null) sr.enabled = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var pc = other.GetComponent<PlayerController>();
        if (pc != null && pc.isMine)
        {
            if (col != null) col.enabled = false;
            if (sr != null) sr.enabled = false;
            UnityClient.Instance.SendItemPickup(instanceId);
        }
    }
}
