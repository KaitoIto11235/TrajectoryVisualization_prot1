using UnityEngine;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System;
using System.Text;
using UnityEngine.XR;
using Valve.VR;

public class FileOperation  // ファイルの読み書きを行う。
{
    private GameObject recordObject;
    private GameObject thisPosIsRecord;
    private bool writeFileOpenFlag = false;
    private string readFileName;
    private int currentFileRowCount;
    private StreamReader sr;
    private StreamWriter sw;
    public Vector3[] modelPositions { get; private set; }
    public Quaternion[] modelQuaternions { get; private set; }
    // public Vector3[] userPositions;

    // recording用変数↓
	private int trialCount = 1;

    // 奇数⇒Training（見本あり）　偶数⇒Test（見本無し）
    public int CurrentTrialCount
    {
        get{ return trialCount; }
    }
    private float time = 0f;        // 1試行中の経過時間
    private float trainingTime = 0f;  // trainigLimit経過したら、テストフェーズに移行。
    private float trainingLimit = 8f;  // 1分間をリミット。
    private bool isTestPhase = false;
    public bool IsTestPhase
    {
        get{ return isTestPhase; }
    }
    private bool isResetTrainingTime = false;  // トレーニングフェーズに入って初めてトリガーを押したときにtrainingTimeを計測しはじめるためのフラグ。
    private bool isReleaseTriger = false;  // トレーニングフェーズ終了時にトリガーは押した状態であるため、その状態でテストフェーズに行かせないためのフラグ。
    public bool fileSettingWrong { get; private set; } = false;


    // InteractUIボタンが押されているかを判定するためのIuiという関数にSteamVR_Actions.defalt_InteractionUIを固定
    private SteamVR_Action_Boolean Iui = SteamVR_Actions.default_InteractUI;
    // 結果の格納用Boolean型変数interacrtui
    private Boolean interactUI;
    public Boolean InteractUI => interactUI;

    // GrabGripボタン（初期設定は側面ボタン）が押されているかを判定するためのGrabという関数にSteamVR_Actions.defalt_GrtabGripを固定
    private SteamVR_Action_Boolean GrabG = SteamVR_Actions.default_GrabGrip;
    // 結果の格納用Boolean型関数grabgrip;
    private Boolean grabGrip;
    private Vector3 RightHandPosition;
    private Vector3 offsetRightHandPosition;
    public Vector3 UserPosition => offsetRightHandPosition;
    private Quaternion RightHandRotationQ;
    private Quaternion offsetRightHandRotationQ;

    private string currentWriteFileNameBase; // 書き込みファイル名のベース
    private bool isRecordingActive = false; // 現在記録中かを示すフラグ
    public bool IsRecordingActive => isRecordingActive; // 外部参照用プロパティ

    // --- 偶数試行の評価用変数 ---
    // private float accumulatedDistance = 0f;
    // private int frameCountForEval = 0;
    // --- ここまで追加 ---

    private List<Vector3> userTrajectory;

    // --- 新しいプロパティ ---
    public int FileRowCount => currentFileRowCount; // 外部参照用プロパティ

    // 静的イベント - フェーズが変わったら通知。
    public static event Action<bool> OnPhaseChanged;

    // --- コンストラクタ修正 ---
    public FileOperation(GameObject recordObj, GameObject thisPosIsRecord)
    {
        this.recordObject = recordObj; // This should be the 'user' GameObject
        this.thisPosIsRecord  = thisPosIsRecord;
        this.userTrajectory = new List<Vector3>();
        this.fileSettingWrong = false; // 初期状態ではエラーなし
        Debug.Log("FileOperation instance created.");
    }

    // 既存のコンストラクタは削除またはコメントアウト
    /*
    // 書き込みなしコンストラクタ
    public FileOperation(string baseWriteFileName, GameObject recordObj, ...) { ... }
    // 読み込みなしコンストラクタ
    public FileOperation(string readFileName, int fileRowCount, GameObject recordObj, ...) { ... }
    // 読み書きありコンストラクタ
    public FileOperation(string readFileName, int fileRowCount, string baseWriteFileName, GameObject recordObj, ...) { ... }
    */

    // --- モデルデータ読み込みメソッド ---
    public bool LoadModelData(string modelFileName, int expectedRowCount)
    {
        this.readFileName = modelFileName; // 内部でファイル名を保持
        this.currentFileRowCount = expectedRowCount; // 期待される行数を設定
        this.fileSettingWrong = false; // 読み込み試行前にエラーフラグをリセット

        // 配列を（再）確保
        modelPositions = new Vector3[this.currentFileRowCount];
        modelQuaternions = new Quaternion[this.currentFileRowCount];

        string filePath = Path.Combine(Application.dataPath, "originalAssets", "File", this.readFileName + ".csv");
        Debug.Log($"Attempting to load model data from: {filePath}");

        if (!File.Exists(filePath))
        {
            Debug.LogError($"Read file not found: {filePath}");
            this.fileSettingWrong = true;
            return false;
        }

        int rowsRead = 0; // 実際に読み込んだデータ行数をカウント
        try
        {
            // usingステートメントでStreamReaderを確実に破棄する
            using (StreamReader tempSr = new StreamReader(filePath, Encoding.UTF8))
            {
                string headerLine = tempSr.ReadLine(); // ヘッダー行を読み飛ばし
                if (headerLine == null)
                {
                    Debug.LogError($"File is empty or header is missing: {filePath}");
                    this.fileSettingWrong = true;
                    return false;
                }
                // Debug.Log($"Header: {headerLine}"); // 必要ならヘッダー内容をログ出力

                // データ行を読み込む
                while (!tempSr.EndOfStream && rowsRead < this.currentFileRowCount) // 配列サイズを超えないように
                {
                    string line = tempSr.ReadLine();
                    string[] values = line.Split(',');

                    // 列数のチェック (最低9列: Trial, time, PosX,Y,Z, QX,Y,Z,W を想定)
                    if (values.Length >= 9)
                    {
                        try
                        {
                            // インデックスは0ベース、ヘッダー順に合わせて調整
                            // 例: PosXが3列目なら values[2]
                            float PosX = float.Parse(values[2].Trim()); // Trim()で空白除去
                            float PosY = float.Parse(values[3].Trim());
                            float PosZ = float.Parse(values[4].Trim());
                            modelPositions[rowsRead] = new Vector3(PosX, PosY, PosZ);

                            float QuaX = float.Parse(values[5].Trim());
                            float QuaY = float.Parse(values[6].Trim());
                            float QuaZ = float.Parse(values[7].Trim());
                            float QuaW = float.Parse(values[8].Trim());
                            // Quaternionの正規化が必要な場合がある
                            modelQuaternions[rowsRead] = new Quaternion(QuaX, QuaY, QuaZ, QuaW).normalized;

                            rowsRead++; // 正常に読み込めたらカウント
                        }
                        catch (FormatException ex)
                        {
                            // パースエラーが発生した行はスキップし、警告を出す
                            Debug.LogWarning($"Skipping line {rowsRead + 2} due to parse error in {filePath}: {ex.Message} - Line: \"{line}\"");
                        }
                        catch (IndexOutOfRangeException ex)
                        {
                             // 列数が足りない行はスキップし、警告を出す
                             Debug.LogWarning($"Skipping line {rowsRead + 2} due to missing columns in {filePath}: {ex.Message} - Line: \"{line}\"");
                        }
                    }
                    else
                    {
                         // 列数が不足している行はスキップし、警告を出す
                         Debug.LogWarning($"Skipping line {rowsRead + 2} due to insufficient columns ({values.Length}) in {filePath}: Expected >= 9. Line: \"{line}\"");
                    }
                } // End of while loop
            } // StreamReader is disposed here

            // 実際に読み込んだ行数を確認
            if (rowsRead != this.currentFileRowCount)
            {
                Debug.LogWarning($"Expected {this.currentFileRowCount} data rows, but successfully read {rowsRead} rows from {filePath}. Adjusting FileRowCount. Check file content or inspector setting.");
                // 実際に読み込めた行数に更新
                this.currentFileRowCount = rowsRead;
                // 配列をリサイズするかどうかは設計による (ここではリサイズしない)
                // 必要ならリサイズ: Array.Resize(ref modelPositions, rowsRead); Array.Resize(ref modelQuaternions, rowsRead);
            }

            // データが1行でも読み込めたか？
            if (rowsRead > 0)
            {
                Debug.Log($"Successfully loaded {rowsRead} data rows from: {filePath}");
                this.fileSettingWrong = false; // エラーフラグを解除
                return true; // 成功
            }
            else
            {
                 // データ行が全く読み込めなかった場合
                 Debug.LogError($"No valid data rows could be read from {filePath}. Check file format.");
                 this.fileSettingWrong = true;
                 return false; // 失敗
            }
        }
        catch (IOException ex) // ファイルI/Oエラー
        {
            Debug.LogError($"IO Error reading file {filePath}: {ex.Message}");
            this.fileSettingWrong = true;
            return false;
        }
        catch (Exception ex) // その他の予期せぬエラー
        {
            Debug.LogError($"Unexpected error reading file {filePath}: {ex.Message}\n{ex.StackTrace}");
            this.fileSettingWrong = true;
            return false;
        }
    }

    // 既存の ReadOpenData は LoadModelData に置き換えられたため削除
    // public void ReadOpenData() { ... }

    // --- 書き込みファイル名設定メソッド ---
    public void SetWriteFileNameBase(string baseName)
    {
        if (string.IsNullOrEmpty(baseName))
        {
             Debug.LogError("Cannot set an empty or null base name for writing files.");
             // this.fileSettingWrong = true; // 必要ならエラーフラグを立てる
             return;
        }
        this.currentWriteFileNameBase = baseName;
        Debug.Log($"Write file base name set to: \"{this.currentWriteFileNameBase}\"");
    }

    // --- 書き込みファイルオープンメソッド ---
    public StreamWriter WriteOpenData()
    {
        if (writeFileOpenFlag) // 既に開いている場合
        {
            Debug.LogWarning("Write file is already open. Returning existing StreamWriter.");
            return sw;
        }

        // ベース名が設定されているか確認
        if (string.IsNullOrEmpty(currentWriteFileNameBase))
        {
            Debug.LogError("Write file base name is not set. Call SetWriteFileNameBase() first.");
            fileSettingWrong = true;
            return null;
        }

        // 試行回数に基づいてファイル名を生成 (Tr:奇数, Te:偶数)
        string suffix = (isTestPhase) ? $"_Te{trialCount/2}" : $"_Tr{(trialCount+1)/2}";
        //string suffix = (trialCount % 2 == 1) ? $"_Tr{(trialCount / 2) + 1}" : $"_Te{trialCount / 2}";
        string currentWriteFileName = $"{currentWriteFileNameBase}{suffix}.csv";
        string filePath = Path.Combine(Application.dataPath, "originalAssets", "File", currentWriteFileName);


        try
        {
            // --- ファイル存在チェック ---
            if (File.Exists(filePath))
            {
                // 既存ファイルを上書きしないようにエラーとする
                Debug.LogError($"Write file already exists: {filePath}. Stopping recording to prevent overwrite. Please rename or delete the existing file.");
                fileSettingWrong = true; // エラーフラグ
                return null; // StreamWriterを返さずに終了
            }
            // --- ここまで ---

            // 新規作成モード (上書きしない: false) でファイルを開く
            sw = new StreamWriter(filePath, false, Encoding.UTF8);

            // ヘッダー行を書き込む
            string[] headerColumns =
            {
                "Trial", "time",
                "PositionX", "PositionY", "PositionZ",
                "RotationQX", "RotationQY", "RotationQZ", "RotationQW",
                // 必要なら他のデータ列も追加
            };
            string headerLine = string.Join(",", headerColumns);
            sw.WriteLine(headerLine);
            sw.Flush(); // ヘッダーはすぐに書き込む

            writeFileOpenFlag = true; // 書き込み成功フラグ

            return sw; // 成功したらStreamWriterを返す
        }
        catch (IOException ex) // ファイルI/Oエラー
        {
            Debug.LogError($"Could not open write file: {filePath}. IO Error: {ex.Message}");
            fileSettingWrong = true;
            if (sw != null) { sw.Dispose(); sw = null; } // エラー時でもリソース解放試行
            return null;
        }
        catch (Exception ex) // その他の予期せぬエラー
        {
            Debug.LogError($"Unexpected error opening write file: {filePath}. Error: {ex.Message}\n{ex.StackTrace}");
            fileSettingWrong = true;
            if (sw != null) { sw.Dispose(); sw = null; }
            return null;
        }
    }



    // --- 記録更新メソッド ---
    public void RecordingUpdate()
    {
        // エラー状態なら何もしない
        if (fileSettingWrong) return;

        // 右コントローラーの入力状態を取得
        interactUI = Iui.GetState(SteamVR_Input_Sources.RightHand);

        // 経過時間を加算
        time += Time.deltaTime;
        // trainingTime += Time.deltaTime;

        // 右コントローラーの現在の姿勢を取得
        // InputDevice rightHandDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        // // TryGetFeatureValue が false を返した場合、デフォルト値または前回値を使う
        // rightHandDevice.TryGetFeatureValue(CommonUsages.devicePosition, out RightHandPosition);
        // rightHandDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out RightHandRotationQ);

        // offsetRightHandPosition = SteamVR_Behaviour_Skeleton.ControllerOffsetPos(RightHandPosition, RightHandRotationQ);

        // RightHandRotationQ = SteamVR_Behaviour_Skeleton.ControllerOffsetRot(RightHandPosition, RightHandRotationQ);
        // //RightHandRotationQ = Quaternion.AngleAxis(90, recordObject.transform.forward ) * RightHandRotationQ;
        // Vector3 euler = RightHandRotationQ.eulerAngles;
        // euler.z -= 90; // z軸回りに-90度回転

        // offsetRightHandRotationQ = Quaternion.Euler(euler);

        RightHandPosition = recordObject.transform.position;
        RightHandRotationQ = recordObject.transform.rotation;

        offsetRightHandPosition = SteamVR_Behaviour_Skeleton.ControllerOffsetPos(RightHandPosition, RightHandRotationQ);
        RightHandRotationQ = SteamVR_Behaviour_Skeleton.ControllerOffsetRot(RightHandPosition, RightHandRotationQ);

        Vector3 euler = RightHandRotationQ.eulerAngles;
        euler.z -= 90;
        offsetRightHandRotationQ = Quaternion.Euler(euler);

        if(!isTestPhase)
        {
            if(!isResetTrainingTime)
            {
                if(interactUI)
                {
                    // 書き込みファイルが開かれていなければ開く試み
                    if (!writeFileOpenFlag)
                    {
                        WriteOpenData();
                        // WriteOpenDataが失敗したら (swがnull or writeFileOpenFlagがfalse)、記録開始しない
                        if (sw == null || !writeFileOpenFlag || fileSettingWrong) {
                            Debug.LogError("Failed to open write file. Recording cannot start.");
                            return; // 記録開始を中断
                        }
                    }
                    // ファイルが正常に開けた場合のみ記録開始処理
                    if (writeFileOpenFlag)
                    {
                        time = 0f; // 記録時間をリセット
                        trainingTime = 0;
                        isResetTrainingTime = true;
                        isRecordingActive = true; // 記録中フラグを立てる
                        userTrajectory.Clear(); // 新しい試行のためにリストをクリア
                        Debug.Log($"Start Recording Trial: {trialCount}");
                        // 記録開始時のデータも保存
                        SaveData(offsetRightHandPosition, offsetRightHandRotationQ);
                        //SaveData(recordObject.transform.position, offsetRightHandRotationQ);
                    }
                }
                else
                {
                    return;
                }
            }

            trainingTime += Time.deltaTime;
            if(trainingTime < trainingLimit)
            {
                // ファイルに書き込む
                if (writeFileOpenFlag && sw != null)
                {
                    SaveData(offsetRightHandPosition, offsetRightHandRotationQ);
                    //SaveData(recordObject.transform.position, offsetRightHandRotationQ);
                }
                else if (!fileSettingWrong) // トリガーを押した状態でファイルが開いていない場合は警告
                {
                    Debug.LogWarning("Cannot save data: Write file is not open or StreamWriter is null during recording.");
                    // isRecordingActive = false; // 記録を中断するなどの措置も検討
                }
            }
            else
            {
                // ファイルを閉じる
                WriteCloseData(); // 現在のファイルを閉じる
                trialCount++;     // 次の試行番号へ

                isRecordingActive = false;
                isTestPhase = true;
                isResetTrainingTime = false;
                isReleaseTriger = false;

                // フェーズの変更を通知
                NotifyPhaseChanged();
            }
        }
        else if(!isReleaseTriger)  // トレーニングフェーズで押しっぱなしのトリガーを離すまでテストフェーズに行かせない。
        {
            if(!interactUI)
            {
                isReleaseTriger = true;
            }
        }
        else
        {
            // --- 記録状態の遷移 ---
            if (interactUI && !isRecordingActive) // トリガーを押した瞬間 (記録開始)
            {
                if(!writeFileOpenFlag) // 書き込みファイルが開かれていなければ開く試み
                {
                    WriteOpenData();
                    // WriteOpenDataが失敗したら (swがnull or writeFileOpenFlagがfalse)、記録開始しない
                    if (sw == null || !writeFileOpenFlag || fileSettingWrong) {
                        Debug.LogError("Failed to open write file. Recording cannot start.");
                        return; // 記録開始を中断
                    }
                }

                // ファイルが正常に開けた場合のみ記録開始処理
                if (writeFileOpenFlag)
                {
                    time = 0f; // 記録時間をリセット
                    userTrajectory.Clear(); // 新しい試行のためにリストをクリア
                    isRecordingActive = true; // 記録中フラグを立てる
                    Debug.Log($"Start Recording Trial: {trialCount}");
                    // 記録開始時のデータも保存
                    SaveData(offsetRightHandPosition, offsetRightHandRotationQ);
                }
            }
            else if (interactUI && isRecordingActive) // トリガーを押している間 (記録中)
            {
                // ファイルが開かれていればデータを保存
                if (writeFileOpenFlag && sw != null)
                {
                    SaveData(offsetRightHandPosition, offsetRightHandRotationQ);
                }
                else if (!fileSettingWrong) // トリガーを押した状態でファイルが開いていない場合は警告
                {
                    Debug.LogWarning("Cannot save data: Write file is not open or StreamWriter is null during recording.");
                    // isRecordingActive = false; // 記録を中断するなどの措置も検討
                }
            }
            else if (!interactUI && isRecordingActive) // トリガーを離した瞬間 (記録終了)
            {
                WriteCloseData(); // 現在のファイルを閉じる
                trialCount++;     // 次の試行番号へ
                isRecordingActive = false; // 記録終了フラグ
                isTestPhase = false;  // テスト終了
                // フェーズの変更を通知
                NotifyPhaseChanged();
            }
        }
        

        

        // エンターキーでの終了処理 (デバッグ用など)
        if (Input.GetKeyDown(KeyCode.Return))
        {
            if (isRecordingActive) {
                Debug.Log("Forcing end of recording via Enter key.");
                // 離した時と同じ処理を実行
                SaveData(offsetRightHandPosition, offsetRightHandRotationQ);
                WriteCloseData();
                trialCount++;
                isRecordingActive = false;
            } else if (writeFileOpenFlag) {
                // 記録中でなくてもファイルが開いている場合があるか？（通常はないはず）
                Debug.LogWarning("Write file was open but not recording. Closing file via Enter key.");
                WriteCloseData();
            }
        }
    }

    // 状態変更を通知
    private void NotifyPhaseChanged()
    {
        // イベントが購読されているか確認してから発火
        OnPhaseChanged?.Invoke(isTestPhase);
    }


    // --- データ保存メソッド ---
    // 引数を pos, rot に変更
    public void SaveData(Vector3 pos, Quaternion rot)
    {
        thisPosIsRecord.transform.position = pos;
        thisPosIsRecord.transform.rotation = rot;
        // 記録中でない、またはファイルが開かれていない場合は書き込まない
        if (!isRecordingActive || sw == null || !writeFileOpenFlag || fileSettingWrong) 
        {
            Debug.Log("SaveData is not active");
            return;
        }
        // 書き込むデータ配列を作成
        string[] dataToWrite = new string[]
        {
            Convert.ToString(trialCount), Convert.ToString(time), // timeは記録開始からの経過時間
            Convert.ToString(pos.x), Convert.ToString(pos.y), Convert.ToString(pos.z),
            Convert.ToString(rot.x), Convert.ToString(rot.y), Convert.ToString(rot.z), Convert.ToString(rot.w),
            // 必要に応じて他のデータも追加
        };

        string lineToWrite = string.Join(",", dataToWrite);

        try
        {
            // ファイルに一行書き込む
            sw.WriteLine(lineToWrite);
            // sw.Flush(); // Flushはパフォーマンス影響を考慮し、通常はClose時に行う

            // ユーザートラジェクトリリストに現在の位置を追加
            // userTrajectory はnullチェック不要 (コンストラクタで初期化されるため)
            userTrajectory.Add(pos);
        }
        catch (ObjectDisposedException)
        {
            // StreamWriterが既に閉じられている場合（通常は起こらないはず）
            Debug.LogError("Cannot write data: StreamWriter has been disposed unexpectedly.");
            isRecordingActive = false; // 記録を強制終了
            fileSettingWrong = true;
        }
        catch (IOException ex)
        {
            Debug.LogError($"IO Error writing data to file: {ex.Message}");
            isRecordingActive = false;
            fileSettingWrong = true;
            WriteCloseData(); // エラー時はファイルを閉じる試み
        }
        catch (Exception ex)
        {
            Debug.LogError($"Unexpected error writing data: {ex.Message}\n{ex.StackTrace}");
            isRecordingActive = false;
            fileSettingWrong = true;
            WriteCloseData();
        }
    }

    // --- ユーザートラジェクトリ取得メソッド ---
    public List<Vector3> GetUserTrajectoryData()
    {
        // 外部で変更されることを防ぐため、リストのコピーを返す
        if (userTrajectory == null)
        {
             Debug.LogError("User trajectory list is null.");
             return new List<Vector3>(); // 空のリストを返す
        }
        return new List<Vector3>(userTrajectory);
    }


    // ReadCloseData は LoadModelData 内で using を使うため不要
    // private void ReadCloseData() { ... }


    // --- 書き込みファイルクローズメソッド ---
    private void WriteCloseData()
    {
        if (sw != null && writeFileOpenFlag) // StreamWriter が存在し、ファイルが開いているフラグが立っている場合のみ
        {
            try
            {
                Debug.Log($"Closing write file. Trial {trialCount} finished.");
                sw.Flush(); // 閉じる前にバッファの内容を確実に書き出す
                sw.Dispose(); // DisposeがCloseも行う (リソース解放)
            }
            catch (ObjectDisposedException)
            {
                // すでに閉じられている場合は警告を出す程度
                Debug.LogWarning("StreamWriter was already disposed when trying to close.");
            }
            catch (IOException ex)
            {
                Debug.LogError($"IO Error closing write file: {ex.Message}");
                // エラーが発生してもフラグはリセットする
            }
            catch (Exception ex)
            {
                Debug.LogError($"Unexpected error closing write file: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                // finallyブロックで確実にnullにし、フラグをリセット
                sw = null;
                writeFileOpenFlag = false;
            }
        }
        else if (sw == null && writeFileOpenFlag)
        {
            // swがnullなのにフラグがtrueになっている異常状態
            Debug.LogWarning("WriteCloseData called but StreamWriter was null while flag was true. Resetting flag.");
            writeFileOpenFlag = false;
        }
        // swがnullでフラグもfalseの場合は何もしない (既に閉じているか、開かれていない)
    }

    // --- public なファイルクローズメソッド ---
    // OnDestroyなどから呼ばれることを想定
    public void CloseFile()
    {
        // 書き込みファイルを閉じる（読み込みファイルはusingで管理）
        WriteCloseData();
        Debug.Log("FileOperation.CloseFile() called.");
    }


} // End of FileOperation class
