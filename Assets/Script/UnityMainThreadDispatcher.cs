using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 다른 스레드에서 Unity 메인 스레드로 작업을 디스패치(전달)하는 클래스
/// Unity UI나 게임 오브젝트 조작은 메인 스레드에서만 가능하므로 필요
/// </summary>
public class UnityMainThreadDispatcher : MonoBehaviour
{
    // 실행할 작업들을 저장하는 큐
    private static readonly Queue<Action> executionQueue = new Queue<Action>();

    private static UnityMainThreadDispatcher instance;

    /// <summary>
    /// 싱글톤 인스턴스를 가져오거나 생성하는 메서드
    /// </summary>
    public static UnityMainThreadDispatcher Instance()
    {
        if (instance == null)
        {
            // 씬에 이미 있는지 확인
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
    /// 다른 스레드에서 실행하고 싶은 작업을 메인 스레드에 등록
    /// </summary>
    public void Enqueue(Action action)
    {
        if (action == null) return;

        lock (executionQueue)
        {
            executionQueue.Enqueue(action);
        }
    }

    // 매 프레임마다 등록된 작업 실행
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
