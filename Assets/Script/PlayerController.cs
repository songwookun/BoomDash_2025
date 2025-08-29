using UnityEngine;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    public bool isMine = false;
    [SerializeField] private float baseSpeed = 5f;

    private float currentSpeed;
    private float minX = -9f, maxX = 9f, minY = -5f, maxY = 4.3f;

    private Bounds forbidden;
    private bool hasForbidden = false;

    private Coroutine speedBuffCo;

    private const float EPS = 0.01f;

    void Awake()
    {
        currentSpeed = baseSpeed;
    }

    public void SetForbiddenBounds(Bounds b)
    {
        forbidden = b;
        hasForbidden = true;
    }

    public void SetPosition(Vector3 p)
    {
        transform.position = p;
    }

    public void ApplySpeedBuff(float add, float duration)
    {
        if (speedBuffCo != null) StopCoroutine(speedBuffCo);
        speedBuffCo = StartCoroutine(SpeedBuffRoutine(add, duration));
    }

    private IEnumerator SpeedBuffRoutine(float add, float duration)
    {
        currentSpeed = baseSpeed + add;
        yield return new WaitForSeconds(duration);
        currentSpeed = baseSpeed;
        speedBuffCo = null;
    }

    void Update()
    {
        if (!isMine) return;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 dir = new Vector3(h, v, 0f).normalized;

        Vector3 next = transform.position + dir * currentSpeed * Time.deltaTime;

        if (hasForbidden && Inside(forbidden, next))
        {
            next = transform.position; 
        }

        next.x = Mathf.Clamp(next.x, minX, maxX);
        next.y = Mathf.Clamp(next.y, minY, maxY);

        transform.position = next;
        UnityClient.Instance?.SendMove(transform.position.x, transform.position.y);
    }

    private bool Inside(Bounds b, Vector3 p)
    {
        return (p.x > b.min.x + EPS && p.x < b.max.x - EPS &&
                p.y > b.min.y + EPS && p.y < b.max.y - EPS);
    }
}
