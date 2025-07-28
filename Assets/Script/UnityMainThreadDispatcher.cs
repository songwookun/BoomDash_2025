using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// �ٸ� �����忡�� Unity ���� ������� �۾��� ����ġ(����)�ϴ� Ŭ����
/// Unity UI�� ���� ������Ʈ ������ ���� �����忡���� �����ϹǷ� �ʿ�
/// </summary>
public class UnityMainThreadDispatcher : MonoBehaviour
{
    // ������ �۾����� �����ϴ� ť
    private static readonly Queue<Action> executionQueue = new Queue<Action>();

    private static UnityMainThreadDispatcher instance;

    /// <summary>
    /// �̱��� �ν��Ͻ��� �������ų� �����ϴ� �޼���
    /// </summary>
    public static UnityMainThreadDispatcher Instance()
    {
        if (instance == null)
        {
            // ���� �̹� �ִ��� Ȯ��
            instance = FindObjectOfType<UnityMainThreadDispatcher>();
            if (instance == null)
            {
                var obj = new GameObject("UnityMainThreadDispatcher");
                instance = obj.AddComponent<UnityMainThreadDispatcher>();
                DontDestroyOnLoad(obj);
            }
        }
        return instance;
    }

    /// <summary>
    /// �ٸ� �����忡�� �����ϰ� ���� �۾��� ���� �����忡 ���
    /// </summary>
    public void Enqueue(Action action)
    {
        if (action == null) return;

        lock (executionQueue)
        {
            executionQueue.Enqueue(action);
        }
    }

    // �� �����Ӹ��� ��ϵ� �۾� ����
    private void Update()
    {
        lock (executionQueue)
        {
            while (executionQueue.Count > 0)
            {
                var action = executionQueue.Dequeue();
                action?.Invoke();
            }
        }
    }
}
