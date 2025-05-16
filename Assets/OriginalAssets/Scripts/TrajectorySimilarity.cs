using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq; // Average()などのために必要

public static class TrajectorySimilarity
{
    // --- DTW評価メソッドの実装 ---
    /// <summary>
    /// DTWを用いてモデル軌跡とユーザー軌跡の類似度を計算します。
    /// 類似度は、DTWパスに沿った位置同士の距離に基づいて計算されます。
    /// </summary>
    /// <param name="modelTrajectoryRaw">モデルの生の軌跡データ (Vector3配列)</param>
    /// <param name="userTrajectoryRaw">ユーザーの生の軌跡データ (Vector3リスト)</param>
    /// <param name="bandWidthPercentage">Sakoe-Chibaバンド幅を軌跡長の割合で指定 (0.0-1.0)。デフォルト0.1 (10%)</param>
    /// <returns>類似度スコア。計算不能な場合は 0.0。</returns>
    public static double CalculateDTWPositionDistanceSimilarity(
        IEnumerable<Vector3> modelTrajectoryRaw,
        IEnumerable<Vector3> userTrajectoryRaw,
        double bandWidthPercentage = 2)
    {
        // 1. 軌跡データを配列に変換
        List<Vector3> modelPositions = new List<Vector3>(modelTrajectoryRaw);
        List<Vector3> userPositions = new List<Vector3>(userTrajectoryRaw);

        if (modelPositions.Count <= 1 || userPositions.Count <= 1)
        {
            Debug.LogWarning("DTW: Not enough position points for calculation.");
            return 0.0;
        }

        // 2. DTW 計算 (Sakoe-Chiba バンド付き)
        int n = modelPositions.Count;
        int m = userPositions.Count;
        // バンド幅を軌跡長の割合で決定 (長い方に合わせる)
        int bandWidth = (int)Math.Max(1, Math.Round(Math.Max(n, m) * bandWidthPercentage));
        //Debug.Log($"DTW Bandwidth: {bandWidth} ({(bandWidthPercentage*100):F1}%) for lengths n={n}, m={m}");

        // コスト行列 (距離) と累積コスト行列の初期化
        // サイズを +1 するのは、トレースバックをしやすくするため（0行目、0列目を番兵として使う）
        double[,] costMatrix = new double[n + 1, m + 1];
        double[,] accumulatedCostMatrix = new double[n + 1, m + 1];

        // 全要素を無限大で初期化
        for (int i = 0; i <= n; i++)
            for (int j = 0; j <= m; j++)
                accumulatedCostMatrix[i, j] = double.PositiveInfinity;

        // 始点 (0,0) のコストは0
        accumulatedCostMatrix[0, 0] = 0;

        // 累積コスト計算 (バンド内)
        for (int i = 1; i <= n; i++)
        {
            // Sakoe-Chibaバンドの範囲を計算
            int jStart = Math.Max(1, i - bandWidth);
            int jEnd = Math.Min(m + 1, i + bandWidth + 1); // +1 はループのため

            for (int j = jStart; j < jEnd; j++)
            {
                // ローカルコスト (位置間のユークリッド距離)
                double localCost = Vector3.Distance(modelPositions[i - 1], userPositions[j - 1]); // インデックス調整
                costMatrix[i, j] = localCost; // 参考用

                // 遷移元コストを取得 (バンド外は無限大)
                double costAbove = accumulatedCostMatrix[i - 1, j];
                double costLeft = accumulatedCostMatrix[i, j - 1];
                double costDiagonal = accumulatedCostMatrix[i - 1, j - 1];

                // 最小コスト遷移を選択
                accumulatedCostMatrix[i, j] = localCost + Math.Min(costAbove, Math.Min(costLeft, costDiagonal));

                // 無限大コストのチェック（通常は発生しないはずだが念のため）
                if (double.IsPositiveInfinity(accumulatedCostMatrix[i,j])) {
                    Debug.LogWarning($"DTW accumulated infinite cost at [{i},{j}].");
                }
            }
        }

        // 3. 最適ワーピングパスのトレースバック
        List<Tuple<int, int>> path = new List<Tuple<int, int>>();
        int currentI = n;
        int currentJ = m;

        // 終点のコストが無限大の場合、パスが見つからなかったことを意味する
        if (double.IsPositiveInfinity(accumulatedCostMatrix[currentI, currentJ])) {
            Debug.LogError("DTW: Path endpoint has infinite cost. No valid path found within the band.");
            return 100.0; // 類似度0
        }

        path.Add(Tuple.Create(currentI - 1, currentJ - 1)); // 0-based index for positions

        while (currentI > 0 || currentJ > 0)
        {
            // 境界に到達した場合
            if (currentI == 0)
            {
                currentJ--;
            }
            else if (currentJ == 0)
            {
                currentI--;
            }
            else // 通常の遷移
            {
                // 遷移元コストを取得 (accumulatedCostMatrix は 1-based index)
                double costAbove = accumulatedCostMatrix[currentI - 1, currentJ];
                double costLeft = accumulatedCostMatrix[currentI, currentJ - 1];
                double costDiagonal = accumulatedCostMatrix[currentI - 1, currentJ - 1];

                // コスト最小の方向に移動
                if (costDiagonal <= costAbove && costDiagonal <= costLeft)
                {
                    currentI--;
                    currentJ--;
                }
                else if (costAbove < costLeft)
                {
                    currentI--; // 上から来た
                }
                else
                {
                    currentJ--; // 左から来た
                }
            }

            // 始点(0,0)に到達したらループを抜ける (理論上は不要だがあっても良い)
            if (currentI == 0 && currentJ == 0) break;

            // パスを追加 (0-based index)
            // ただし、(0,0)からの遷移は含めないように currentI > 0 or currentJ > 0 の条件がある
            if (currentI > 0 || currentJ > 0) { // currentI, currentJ は 1-based index 思考
                path.Add(Tuple.Create(currentI - 1, currentJ - 1));
            }
        }
        path.Reverse(); // パスを始点から終点の順にする

        if (path.Count == 0) {
            Debug.LogError("DTW: Failed to reconstruct the warping path.");
            return 0.0;
        }

        // 4. ワーピングパスに沿った距離を計算
        List<double> distances = new List<double>();
        foreach (var point in path)
        {
            int modelIdx = point.Item1;
            int userIdx = point.Item2;

            // パスのインデックスが位置リストの範囲内か確認
            if (modelIdx >= 0 && modelIdx < modelPositions.Count && userIdx >= 0 && userIdx < userPositions.Count)
            {
                Vector3 modelPos = modelPositions[modelIdx];
                Vector3 userPos = userPositions[userIdx];
                
                // 位置間の距離を計算
                double distance = Vector3.Distance(modelPos, userPos);
                distances.Add(distance);
            }
            else
            {
                Debug.LogWarning($"DTW Path index out of range: modelIdx={modelIdx}, userIdx={userIdx}");
            }
        }

        // 5. 距離の平均値を計算
        if (distances.Count == 0)
        {
            Debug.LogWarning("DTW: No valid distances calculated along the path.");
            return 0.0;
        }
        
        // 距離の平均値（小さいほど類似）を計算
        double averageDistance = distances.Average();
        
        // 注意: 距離は小さいほど類似しているため、必要に応じて類似度スコアへの変換が必要
        // 例: double similarityScore = 1.0 / (1.0 + averageDistance); // 0〜1の範囲に変換する例
        
        return averageDistance; // 距離を直接返す（小さいほど類似度が高い）
    }

    // 元のメソッドとの互換性のために残しておく
    public static double CalculateDTWDotProductSimilarity(
        IEnumerable<Vector3> modelTrajectoryRaw,
        IEnumerable<Vector3> userTrajectoryRaw,
        double bandWidthPercentage = 0.1)
    {
        // 1. ベクトル化と非ゼロ抽出
        List<Vector3> modelVectors = ExtractNonZeroVectors(modelTrajectoryRaw);
        List<Vector3> userVectors = ExtractNonZeroVectors(userTrajectoryRaw);

        if (modelVectors.Count == 0 || userVectors.Count == 0)
        {
            Debug.LogWarning("DTW: Not enough non-zero vectors for calculation.");
            return 0.0;
        }

        // 2. DTW 計算 (Sakoe-Chiba バンド付き)
        int n = modelVectors.Count;
        int m = userVectors.Count;
        // バンド幅を軌跡長の割合で決定 (短い方に合わせるか、平均にするかなど選択肢あり。ここでは長い方に合わせる)
        int bandWidth = (int)Math.Max(1, Math.Round(Math.Max(n, m) * bandWidthPercentage));
        //Debug.Log($"DTW Bandwidth: {bandWidth} ({(bandWidthPercentage*100):F1}%) for lengths n={n}, m={m}");


        // コスト行列 (距離) と累積コスト行列の初期化
        // サイズを +1 するのは、トレースバックをしやすくするため（0行目、0列目を番兵として使う）
        double[,] costMatrix = new double[n + 1, m + 1];
        double[,] accumulatedCostMatrix = new double[n + 1, m + 1];

        // 全要素を無限大で初期化
        for (int i = 0; i <= n; i++)
            for (int j = 0; j <= m; j++)
                accumulatedCostMatrix[i, j] = double.PositiveInfinity;

        // 始点 (0,0) のコストは0
        accumulatedCostMatrix[0, 0] = 0;


        // 累積コスト計算 (バンド内)
        for (int i = 1; i <= n; i++)
        {
            // Sakoe-Chibaバンドの範囲を計算
            int jStart = Math.Max(1, i - bandWidth);
            int jEnd = Math.Min(m + 1, i + bandWidth + 1); // +1 はループのため

            for (int j = jStart; j < jEnd; j++)
            {
                // ローカルコスト (ベクトル間のユークリッド距離)
                double localCost = Vector3.Distance(modelVectors[i - 1], userVectors[j - 1]); // インデックス調整
                costMatrix[i, j] = localCost; // 参考用

                // 遷移元コストを取得 (バンド外は無限大)
                double costAbove = accumulatedCostMatrix[i - 1, j];
                double costLeft = accumulatedCostMatrix[i, j - 1];
                double costDiagonal = accumulatedCostMatrix[i - 1, j - 1];

                 // 最小コスト遷移を選択
                 accumulatedCostMatrix[i, j] = localCost + Math.Min(costAbove, Math.Min(costLeft, costDiagonal));

                 // 無限大コストのチェック（通常は発生しないはずだが念のため）
                if (double.IsPositiveInfinity(accumulatedCostMatrix[i,j])) {
                     Debug.LogWarning($"DTW accumulated infinite cost at [{i},{j}].");
                }
            }
        }


        // 3. 最適ワーピングパスのトレースバック
        List<Tuple<int, int>> path = new List<Tuple<int, int>>();
        int currentI = n;
        int currentJ = m;

        // 終点のコストが無限大の場合、パスが見つからなかったことを意味する
        if (double.IsPositiveInfinity(accumulatedCostMatrix[currentI, currentJ])) {
             Debug.LogError("DTW: Path endpoint has infinite cost. No valid path found within the band.");
             return 0.0; // 類似度0
        }


        path.Add(Tuple.Create(currentI - 1, currentJ - 1)); // 0-based index for vectors

        while (currentI > 0 || currentJ > 0)
        {
            // 境界に到達した場合
            if (currentI == 0)
            {
                currentJ--;
            }
            else if (currentJ == 0)
            {
                currentI--;
            }
            else // 通常の遷移
            {
                 // 遷移元コストを取得 (accumulatedCostMatrix は 1-based index)
                double costAbove = accumulatedCostMatrix[currentI - 1, currentJ];
                double costLeft = accumulatedCostMatrix[currentI, currentJ - 1];
                double costDiagonal = accumulatedCostMatrix[currentI - 1, currentJ - 1];

                // コスト最小の方向に移動
                if (costDiagonal <= costAbove && costDiagonal <= costLeft)
                {
                    currentI--;
                    currentJ--;
                }
                else if (costAbove < costLeft)
                {
                    currentI--; // 上から来た
                }
                else
                {
                    currentJ--; // 左から来た
                }
            }

            // 始点(0,0)に到達したらループを抜ける (理論上は不要だがあっても良い)
             if (currentI == 0 && currentJ == 0) break;

            // パスを追加 (0-based index)
            // ただし、(0,0)からの遷移は含めないように currentI > 0 or currentJ > 0 の条件がある
            if (currentI > 0 || currentJ > 0) { // currentI, currentJ は 1-based index 思考
                 path.Add(Tuple.Create(currentI - 1, currentJ - 1));
            }
        }
        path.Reverse(); // パスを始点から終点の順にする

        if (path.Count == 0) {
             Debug.LogError("DTW: Failed to reconstruct the warping path.");
             return 0.0;
        }

        // 4. ワーピングパスに沿った正規化内積の計算
        List<double> dotProducts = new List<double>();
        foreach (var point in path)
        {
            int modelIdx = point.Item1;
            int userIdx = point.Item2;

            // パスのインデックスがベクトルリストの範囲内か確認
            if (modelIdx >= 0 && modelIdx < modelVectors.Count && userIdx >= 0 && userIdx < userVectors.Count)
            {
                Vector3 modelVec = modelVectors[modelIdx];
                Vector3 userVec = userVectors[userIdx];

                float modelNorm = modelVec.magnitude;
                float userNorm = userVec.magnitude;

                // ゼロベクトルでないことを確認
                if (modelNorm > float.Epsilon && userNorm > float.Epsilon)
                {
                    double dotProduct = Vector3.Dot(modelVec, userVec);
                    double normalizedDotProduct = dotProduct / (modelNorm * userNorm);
                    // Clamp to [-1, 1] due to potential floating point inaccuracies
                    normalizedDotProduct = Math.Max(-1.0, Math.Min(1.0, normalizedDotProduct));
                    dotProducts.Add(normalizedDotProduct);
                }
                 // else { Debug.Log($"Skipping zero vector at path point: modelIdx={modelIdx}, userIdx={userIdx}"); }
            }
            else
            {
                 Debug.LogWarning($"DTW Path index out of range: modelIdx={modelIdx}, userIdx={userIdx}");
            }
        }

        // 5. 正規化内積の平均値を返す
        if (dotProducts.Count == 0)
        {
            Debug.LogWarning("DTW: No valid dot products calculated along the path.");
            return 0.0;
        }
        return dotProducts.Average();
    }

    // --- ヘルパーメソッド: 軌跡から非ゼロベクトルを抽出 ---
    /// <summary>
    /// 生の軌跡データから、フレーム間の非ゼロ移動ベクトルを抽出します。
    /// </summary>
    private static List<Vector3> ExtractNonZeroVectors(IEnumerable<Vector3> trajectory)
    {
        List<Vector3> vectors = new List<Vector3>();
        Vector3? previousPoint = null;

        foreach (Vector3 currentPoint in trajectory)
        {
            if (previousPoint.HasValue)
            {
                Vector3 diff = currentPoint - previousPoint.Value;
                // 非常に小さいベクトルは無視 (ゼロベクトルとみなす)
                if (diff.magnitude > 1e-5f) // 閾値を調整 (float.Epsilonより少し大きい値)
                {
                    vectors.Add(diff);
                }
                 // else { Debug.Log($"Ignoring near-zero vector: {diff.magnitude}"); }
            }
            previousPoint = currentPoint;
        }
         // Debug.Log($"Extracted {vectors.Count} non-zero vectors from {trajectory.Count()} points.");
        return vectors;
    }
}