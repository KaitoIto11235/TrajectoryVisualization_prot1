using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LightColorManager : MonoBehaviour
{
    // FileOperationクラスのisTestPhaseの現在値
    private bool currentPhaseValue = false;
    Light lt;

    void OnEnable()
    {
        // イベントを購読
        FileOperation.OnPhaseChanged += OnPhaseChangedHandler;
    }

    void OnDisable()
    {
        // イベント購読解除
        FileOperation.OnPhaseChanged -= OnPhaseChangedHandler;
    }

    // イベントハンドラ
    private void OnPhaseChangedHandler(bool newPhaseValue)
    {
        // 値を更新して保存
        currentPhaseValue = newPhaseValue;
        
        
        // 更新された値を使用して処理を実行
        HandlePhaseChange();
    }

    // フェーズ変更時の処理
    private void HandlePhaseChange()
    {
        if (currentPhaseValue)
        {
            lt.color = Color.red;
        }
        else
        {
            lt.color = Color.white;
        }
    }

    void Start()
    {
        lt = gameObject.GetComponent<Light>();
    }

}
