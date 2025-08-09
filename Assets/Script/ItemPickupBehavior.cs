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
        sr = GetComponent<SpriteRenderer>() ?? gameObject.AddComponent<SpriteRenderer>();
        col.enabled = true;
        if (sr) sr.enabled = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var pc = other.GetComponent<PlayerController>();
        if (pc != null && pc.isMine)
        {
            if (!GameManager.Instance.TryPickupLocally(instanceId))
                return;
            UnityClient.Instance.SendItemPickup(instanceId);
        }
    }

}
