using UnityEngine;
using Newtonsoft.Json;

public class PlayerController : MonoBehaviour
{
    public bool isMine = false;
    public int playerId = 0;
    private float speed;
    private float minX, maxX, minY, maxY;

    private Bounds forbidden;
    private bool hasForbidden = false;
    private const float EPS = 0.01f;

    public void SetForbiddenBounds(Bounds b)
    {
        forbidden = b;
        hasForbidden = true;
    }

    private bool Inside(Bounds b, Vector3 p)
    {
        return (p.x > b.min.x && p.x < b.max.x && p.y > b.min.y && p.y < b.max.y);
    }
    private Vector3 PushOutOf(Bounds b, Vector3 p)
    {
        float dl = Mathf.Abs(p.x - b.min.x);
        float dr = Mathf.Abs(b.max.x - p.x);
        float db = Mathf.Abs(p.y - b.min.y);
        float dt = Mathf.Abs(b.max.y - p.y);

        float m = Mathf.Min(dl, Mathf.Min(dr, Mathf.Min(db, dt)));

        if (m == dl) p.x = b.min.x - EPS;
        else if (m == dr) p.x = b.max.x + EPS;
        else if (m == db) p.y = b.min.y - EPS;
        else p.y = b.max.y + EPS;

        return p;
    }

    void Start()
    {
        speed = PlayerStatLoader.GetMoveSpeed(playerId);
        Debug.Log($"[PlayerController] ID {playerId} 이동속도: {speed}");

        Camera cam = Camera.main;
        float vertExtent = cam.orthographicSize;
        float horzExtent = vertExtent * cam.aspect;
        Vector3 camPos = cam.transform.position;

        minX = camPos.x - horzExtent;
        maxX = camPos.x + horzExtent;
        minY = camPos.y - vertExtent;
        maxY = camPos.y + vertExtent;
    }

    void Update()
    {
        if (!isMine) return;

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 dir = new Vector3(h, v, 0);

        if (dir.magnitude > 0.01f)
        {
            transform.position += dir * speed * Time.deltaTime;

            Vector3 next = transform.position;
            next.x = Mathf.Clamp(next.x, minX, maxX);
            next.y = Mathf.Clamp(next.y, minY, maxY);

            if (hasForbidden && Inside(forbidden, next))
                next = PushOutOf(forbidden, next);

            transform.position = next;

            UnityClient.Instance.SendMove(transform.position.x, transform.position.y);
        }
    }

    public void SetPosition(Vector3 pos)
    {
        if (hasForbidden && Inside(forbidden, pos))
            pos = PushOutOf(forbidden, pos);

        transform.position = pos;
    }
}
