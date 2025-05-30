using UnityEngine;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System;
using System.Text;

public class TrajToAutoCondition : MonoBehaviour
{
    [SerializeField] AudioSource audioSource;
    [SerializeField] private GameObject guidance, user;
    [SerializeField] string readFileName = "default";
    [SerializeField] string writeFileName = "default";
    [SerializeField] int readFileRowCount = 1000;
    public static FileOperation trajToAutoFile;
    AutoPlay autoGuidance;
    [SerializeField] bool Recording = false;
    [SerializeField] Material[] materialArray = new Material[3];
    int commaPlaySpeed = 10; // 10が等速再生
    //[SerializeField, Range(1, 20)] int commaPlaySpeed = 10;

    [SerializeField] GameObject wristR;
    [SerializeField] Animator p_Animator;
    [SerializeField] bool is3PP;

    [Tooltip("If not set, relative to parent")]
    public Transform origin;


    public TransparencyManager prefabTransparencyManager;
    private bool preFramePhase = false;  // 前フレームのフェーズを保存。テストフェーズへの切り替えを判定するために使用。
    [SerializeField] GameObject thisPosIsRecord;  // このゲームオブジェクトの位置や回転がファイルに書かれる。
    

    void Start()
    {
        prefabTransparencyManager = new TransparencyManager();  // インスタンス作成
        prefabTransparencyManager.MakeTaggedObjectsTransparent("AutoModel");  // 初めはAutoModelを非表示に。

        // 1. 新しいコンストラクタでインスタンス作成
        try
        {
            trajToAutoFile = new FileOperation(user, thisPosIsRecord);
        }
        catch (Exception ex)
        {
             Debug.LogError($"Failed to instantiate FileOperation: {ex.Message} {ex.StackTrace}", this);
             enabled = false; // エラー時はコンポーネントを無効化
             return;
        }

        // 2. モデルデータ読み込み (ファイル名が指定されている場合のみ)
        bool modelLoaded = false;
        if (!string.IsNullOrEmpty(readFileName))
        {
            if (!trajToAutoFile.LoadModelData(readFileName, readFileRowCount))
            {
                 Debug.LogError($"Failed to load model data: {readFileName}. Disabling component.", this);
                 enabled = false;
                 return;
            }
            else
            {
                 // AutoPlayの初期化に必要なreadFileRowCountを、実際に読み込んだ行数に更新
                 // (LoadModelData内で不一致時に更新される可能性があるため)
                 readFileRowCount = trajToAutoFile.FileRowCount;
                 modelLoaded = true;
            }
        }
        else
        {
             Debug.LogWarning("readFileName is not set. Skipping model loading.", this);
             // モデルがない場合、AutoPlayの初期化は行わない
        }

        // 3. 書き込みファイル名設定 (Recordingがtrueの場合のみ)
        if (Recording)
        {
            if (!string.IsNullOrEmpty(writeFileName))
            {
                trajToAutoFile.SetWriteFileNameBase(writeFileName);
            }
            else
            {
                 Debug.LogError("Recording is enabled but writeFileName is not set. Recording might fail.", this);
                 // 必要なら enabled = false; return; などで停止
            }
            // WriteOpenData は RecordingUpdate 内で必要に応じて呼ばれるため、ここでは呼ばない
        }

        if (modelLoaded && trajToAutoFile != null)
        {
            try
            {
                // AutoPlayのコンストラクタ引数から materialArray を削除 (AutoPlay側で修正済みのはず)
                // FileRowCountは実際にロードされた行数を使う
                autoGuidance = new AutoPlay(guidance, user, trajToAutoFile.FileRowCount, trajToAutoFile.modelPositions, trajToAutoFile.modelQuaternions,
                                       commaPlaySpeed, /* materialArray, */ wristR, p_Animator);
                Debug.Log("AutoPlay initialized successfully.");
            }
            catch (Exception ex)
            {
                 Debug.LogError($"Failed to initialize AutoPlay: {ex.Message} {ex.StackTrace}", this);
                 enabled = false;
                 return;
            }
        }
        else if (trajToAutoFile == null)
        {
            Debug.LogError("FileOperation instance (trajToAutoFile) is null after initialization attempt.", this);
            enabled = false;
            return;
        }
        // readFileName が空で modelLoaded が false の場合は AutoPlay を初期化しない（上のログで警告済み）


        // 初期マテリアルを設定
        // UpdateMaterial();
    }

    void FixedUpdate()
    {
        if (trajToAutoFile.fileSettingWrong)
        {
            Debug.LogError("File setting check failed after initialization. Disabling component.", this);
            enabled = false;
            OnDestroy();
            UnityEditor.EditorApplication.isPlaying = false;
            return;
        }
        if(autoGuidance != null)
        {
            autoGuidance.GuidanceUpdate();
        }

        // 効果音 (GuidanceTime を CurrentGuidanceTime に変更)
        if(autoGuidance != null && (autoGuidance.CurrentGuidanceTime+1) % 90 == 0 && autoGuidance.CurrentGuidanceTime <= 721 && autoGuidance.CurrentGuidanceTime > -1)
        {
            audioSource.Play();
        }

        // RecordingUpdate はユーザーの位置更新と記録状態の管理を行うため、そのまま呼び出す
        if (Recording && trajToAutoFile != null)
        { 
            trajToAutoFile.RecordingUpdate();
            bool ableChangeMaterial = true;
            // フェーズが前フレームと異なっていたらマテリアルを更新
            if(trajToAutoFile.IsTestPhase != preFramePhase)
            {
                if(!trajToAutoFile.IsTestPhase)
                {
                    List<Vector3> userTrajectory = trajToAutoFile.GetUserTrajectoryData();
                    double similarityScore = TrajectorySimilarity.CalculateDTWPositionDistanceSimilarity(trajToAutoFile.modelPositions, userTrajectory);
                    Debug.Log("similarityScore: " + similarityScore);
                }
                else
                {
                    Debug.Log("StartTestPhase");
                }
                
            }
            preFramePhase = trajToAutoFile.IsTestPhase;

            // トレーニング時はマテリアルがトリガーによって変わる
            if(trajToAutoFile.IsTestPhase)
            {
                ableChangeMaterial = false;
            }
            UpdateMaterial(ableChangeMaterial);
        }
    }

    // マテリアルを更新するメソッド
    void UpdateMaterial(bool ableChange)
    {
        if(ableChange)
        {
            // "TransparentTarget"タグを持つオブジェクトを一括で透明化。
            if (!trajToAutoFile.InteractUI == true)
            {
                prefabTransparencyManager.MakeTaggedObjectsTransparent("Trajectory");
                prefabTransparencyManager.MakeTaggedObjectsTransparent("AutoModel");
                
                // if (trajToAutoFile.CurrentTrialCount <= 20)  // Trajectoryトレーニングでテストを10(=20/2)回行ったら、AutoModelを表示。
                // {
                //     prefabTransparencyManager.MakeTaggedObjectsTransparent("Trajectory");
                // }
                // else
                // {
                //     prefabTransparencyManager.MakeTaggedObjectsTransparent("AutoModel");
                // }

            }
            else  // "TransparentTarget"タグを持つオブジェクトのマテリアルを元に戻す。
            {
                prefabTransparencyManager.RestoreObjects("Trajectory");
                // if (trajToAutoFile.CurrentTrialCount <= 20)
                // {
                //     prefabTransparencyManager.RestoreObjects("Trajectory");
                // }
                // else
                // {
                //     prefabTransparencyManager.RestoreObjects("AutoModel");
                // }
            }
        }
        else
        {
            prefabTransparencyManager.MakeTaggedObjectsTransparent("Trajectory");
            prefabTransparencyManager.MakeTaggedObjectsTransparent("AutoModel");
        }
        
    }

    void OnAnimatorIK()
    {
        if (autoGuidance == null || p_Animator == null) return; // nullチェックを追加

        Vector3 TargetPos = Vector3.zero; // 初期化
        Quaternion TargetRot = Quaternion.identity; // 初期化

        // guidanceTime の取得箇所で CurrentGuidanceTime を使う
        int guidanceTime = (autoGuidance != null) ? autoGuidance.CurrentGuidanceTime : 0; // Nullチェック & プロパティ名変更
        int modelIndex = -1; // 初期化
        // FileOperation側のプロパティ名変更に合わせて FileRowCount を使う
        if (trajToAutoFile != null && trajToAutoFile.modelPositions != null && trajToAutoFile.FileRowCount > 0) {
             modelIndex = Math.Min(Math.Max(0, guidanceTime), trajToAutoFile.FileRowCount - 1); // 配列長ではなくFileRowCountで制限
        } else {
            // Debug.LogWarning("modelPositions is null or FileRowCount is zero, cannot calculate modelIndex.");
             return; // 配列がないか行数が0ならIK設定をスキップ
        }


        // ModelPositions と ModelQuaternions が null でなく、インデックスが有効な範囲にあるか再確認
        if (modelIndex != -1 && trajToAutoFile.modelQuaternions != null && modelIndex < trajToAutoFile.modelQuaternions.Length)
        {
            if(origin == null)
            {
                float three = 0;
                if(is3PP == true)
                {
                    three = 3;
                }
                Vector3 offset = new Vector3(0, 0, three);
                TargetPos = trajToAutoFile.modelPositions[modelIndex] + offset; // autoGuidance経由ではなくautoFileから直接アクセス
                TargetRot = trajToAutoFile.modelQuaternions[modelIndex]; // autoGuidance経由ではなくautoFileから直接アクセス
            }
            else
            {
                TargetPos = origin.TransformPoint(trajToAutoFile.modelPositions[modelIndex]);
                TargetRot = origin.rotation * trajToAutoFile.modelQuaternions[modelIndex];
            }
        } else {
             // データがない場合のフォールバック処理
             // Debug.LogWarning($"IK target data is not available for index {modelIndex}.");
             return; // データがない場合はIK設定をスキップ
        }

        // IK設定 (既存のコード)
        p_Animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 1.0f);
        p_Animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 1.0f);
        p_Animator.SetIKPosition(AvatarIKGoal.RightHand, TargetPos);
        p_Animator.SetIKRotation(AvatarIKGoal.RightHand, TargetRot);
    }

    void OnDestroy()
    {
        if (trajToAutoFile != null && Recording)
        {
           trajToAutoFile.CloseFile();
        }
    }
}


