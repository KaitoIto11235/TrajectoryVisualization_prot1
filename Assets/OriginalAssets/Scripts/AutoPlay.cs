using UnityEngine;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System;
using System.Text;
using Valve.VR;

public class AutoPlay : BaseGuidance // ガイダンスに関する計算・処理を行う。
{
    //private int correspondTime = 0;  // Userの現在地に対応するModelの時間。 値が-1のとき、試行と試行の間であることを意味する
    private int guidanceTime = 0;   // ガイダンスの現在の時間。
    // public int GuidanceTime // プロパティ名を変更
    public int CurrentGuidanceTime
    {
        get {return guidanceTime;}
    }

    private float playSpeed = 1f;
    private float forSpeedChange = 0f;

    // InteractUIボタンが押されているかを判定するためのIuiという関数にSteamVR_Actions.defalt_InteractionUIを固定
    private SteamVR_Action_Boolean Iui = SteamVR_Actions.default_InteractUI;
    // 結果の格納用Boolean型変数interacrtui
    private Boolean interactUI;

    // GrabGripボタン（初期設定は側面ボタン）が押されているかを判定するためのGrabという関数にSteamVR_Actions.defalt_GrtabGripを固定
    private SteamVR_Action_Boolean GrabG = SteamVR_Actions.default_GrabGrip;
    // 結果の格納用Boolean型関数grabgrip;
    private Boolean grabGrip;
    private GameObject wristR; // 右手首の参照 (IKやオフセット計算に使用？)
    private Animator p_Animator; // プレイヤーモデルのアニメーター
    private bool interactOnce = false; // トリガーが一度でも押されたか
    // public int trialCount = 1; // 試行回数は FileOperation で管理するため、ここでは不要

    // --- ローカル変数としてモデルの行数を保持 ---
    private int currentModelRowCount = 0;

    // --- コンストラクタ修正 ---
    // Material配列はStoCConditionで管理するため、引数から削除
    public AutoPlay(GameObject guidance, GameObject user, int modelRowCount, Vector3[] positions, Quaternion[] quaternions,
     int commaPlaySpeed, /* Material[] materialArray,*/ GameObject wristR, Animator p_Animator)
        : base(guidance, user, modelRowCount, positions, quaternions, null) // BaseGuidance には null を渡す
    {
        // BaseGuidanceのフィールドにアクセス可能と仮定
        // base.guidanceObject = guidance;
        // base.userObject = user;
        base.fileRowCount = modelRowCount;
        base.modelPositions = positions;
        base.modelQuaternions = quaternions;
        // base.guidanceMaterials = null;

        this.currentModelRowCount = modelRowCount; // ローカルにも保持
        this.playSpeed = Mathf.Clamp((float)commaPlaySpeed / 10f, 0.1f, 2.0f); // 速度に下限上限を設定
        this.wristR = wristR;
        this.p_Animator = p_Animator;
        this.guidanceTime = 0; // 初期化
        this.forSpeedChange = 0f;
        this.interactOnce = false;
    }

    // --- モデルデータ更新メソッド ---
    public void UpdateModelData(Vector3[] newPositions, Quaternion[] newQuaternions, int newRowCount)
    {
        // BaseGuidance のフィールドを更新
        // BaseGuidanceのフィールドが private の場合は public setter か protected アクセス修飾子が必要
        if (base.modelPositions == null || base.modelQuaternions == null)
        {
            Debug.LogError("BaseGuidance fields (modelPositions/Quaternions) are null. Cannot update.");
            return;
        }

        // baseクラスのフィールドを直接更新 (アクセス可能と仮定)
        base.modelPositions = newPositions;
        base.modelQuaternions = newQuaternions;
        base.fileRowCount = newRowCount;

        // このクラスのローカル変数も更新
        this.currentModelRowCount = newRowCount;

        // ガイダンス時間と再生関連の状態をリセット
        this.guidanceTime = 0;
        this.forSpeedChange = 0f;
        this.interactOnce = false; // リセット

        Debug.Log($"AutoPlay model data updated. New row count: {this.currentModelRowCount}");
    }


    // --- Evaluation と Moving は BaseGuidance から継承されているが、ここでは使わない想定 ---
    public override float Evaluation()
    {
        // 呼び出されない想定
        Debug.LogWarning("AutoPlay.Evaluation() called unexpectedly.");
        return -1f;
    }

    public override void Moving(int updateCount)
    {
        // 呼び出されない想定
        Debug.LogWarning("AutoPlay.Moving() called unexpectedly.");
    }


    // --- ガイダンス時間更新メソッド ---
    public override void GuidanceUpdate()
    {
        // モデルデータがない場合は何もしない
        if (currentModelRowCount <= 0) return;

        // 右コントローラーのトリガー入力取得
        interactUI = Iui.GetState(SteamVR_Input_Sources.RightHand);

        if (interactUI) // トリガーが押されている間
        {
            // 現在のガイダンス時間がモデルデータの範囲内か確認
            if(guidanceTime < currentModelRowCount) // 最後のフレーム(RowCount-1)まで進めるように < を使う
            {
                // 再生速度に応じて時間経過を計算
                forSpeedChange += playSpeed * Time.deltaTime * 90f; // 90Hz想定？ Time.deltaTime を使う方がフレームレートに依存しない
                                                                    // 元のコードに合わせるなら forSpeedChange += playSpeed;

                if(forSpeedChange >= 1.0f)
                {
                    int increment = (int)forSpeedChange; // 進めるフレーム数
                    // 次の時間がモデル行数を超えないように制限
                    // Math.Min(guidanceTime + increment, currentModelRowCount) だと RowCount自体には到達しない
                    // Math.Min(guidanceTime + increment, currentModelRowCount - 1) が正しい最大インデックス
                    guidanceTime = Math.Min(guidanceTime + increment, currentModelRowCount -1);

                    forSpeedChange -= increment; // 整数部分を減算
                }
            }
            // else: 既に終端に達している場合
            // { guidanceTime = currentModelRowCount - 1; } // 終端で止め続ける

            interactOnce = true; // 一度でも押されたフラグ
        }
        else // トリガーが離されている間
        {
            // 直前まで押されていた場合 (離した瞬間)
            if(interactOnce == true)
            {
                // FileOperation側で試行回数は管理されるため、ここではリセットのみ
                // trialCount++;
                guidanceTime = 0; // ガイダンス時間をリセット
                forSpeedChange = 0f; // 速度変化量もリセット
                interactOnce = false; // フラグをリセット
                 // Debug.Log("Trigger released, resetting guidance time.");
            }
            // それ以外（ずっと離されている）場合は何もしない
        }
         // デバッグ用に現在の時間を表示
         // Debug.Log($"Guidance Time: {guidanceTime} / {currentModelRowCount - 1}");
    }

    // GuidanceTimeプロパティは CurrentGuidanceTime に変更済み
    // TrialCount は削除

}