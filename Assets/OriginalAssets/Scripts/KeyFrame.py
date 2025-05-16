import numpy as np
import matplotlib.pyplot as plt
import pandas as pd
from scipy.spatial.transform import Rotation as R
from scipy.spatial.transform import Slerp

def distance_point_to_line_segment(point, line_start, line_end):
    """点から線分への最短距離を計算する"""
    line_vec = line_end - line_start
    point_vec = point - line_start
    line_length = np.linalg.norm(line_vec)
    
    # 線分の長さがほぼ0の場合は、点から始点への距離を返す
    if line_length < 1e-6:
        return np.linalg.norm(point_vec)
    
    # 射影の割合を計算（線分のパラメータ t）
    t = np.dot(point_vec, line_vec) / (line_length * line_length)
    t = max(0, min(1, t))  # tを[0,1]の範囲に制限
    
    # 射影点を計算
    projection = line_start + t * line_vec
    
    # 点から射影点までの距離を返す
    return np.linalg.norm(point - projection)

def find_max_distance_point(positions, start_idx, end_idx):
    """2つのキーフレーム間で線分から最も遠い点のインデックスを見つける"""
    if end_idx - start_idx <= 1:  # 間に点がない場合
        return None, 0.0
    
    start_pos = positions[start_idx]
    end_pos = positions[end_idx]
    
    max_distance = 0.0
    max_distance_idx = None
    
    # start_idx+1 から end_idx-1 までの各点について
    for i in range(start_idx + 1, end_idx):
        distance = distance_point_to_line_segment(positions[i], start_pos, end_pos)
        if distance > max_distance:
            max_distance = distance
            max_distance_idx = i
    
    return max_distance_idx, max_distance

def recursive_add_keyframes(positions, existing_keyframes, threshold=0.1, max_depth=10):
    """再帰的にキーフレームを追加する関数"""
    if max_depth <= 0:
        return existing_keyframes
    
    new_keyframes = existing_keyframes.copy()
    added = False
    
    # 既存のキーフレーム間で最大距離の点を探す
    for i in range(len(existing_keyframes) - 1):
        start_idx = existing_keyframes[i]
        end_idx = existing_keyframes[i+1]
        
        max_dist_idx, max_distance = find_max_distance_point(positions, start_idx, end_idx)
        
        # 閾値より大きい距離の点があればキーフレームに追加
        if max_dist_idx is not None and max_distance > threshold:
            new_keyframes.append(max_dist_idx)
            added = True
    
    # キーフレームが追加された場合、ソートして再帰的に続ける
    if added:
        new_keyframes = sorted(new_keyframes)
        return recursive_add_keyframes(positions, new_keyframes, threshold, max_depth - 1)
    else:
        return new_keyframes

# メイン処理
def find_optimal_keyframes(input_file, initial_keyframes, distance_threshold=0.1, max_recursive_depth=5):
    """最適なキーフレームを見つける関数"""
    try:
        # CSVファイルを読み込む
        df = pd.read_csv(input_file)
        
        # 位置データを取得
        x = df['PositionX'].values
        y = df['PositionY'].values
        z = df['PositionZ'].values
        
        # 位置データを (N, 3) の形状にする
        positions = np.vstack([x, y, z]).T
        
        # 再帰的にキーフレームを追加
        optimal_keyframes = recursive_add_keyframes(
            positions, 
            initial_keyframes, 
            threshold=distance_threshold, 
            max_depth=max_recursive_depth
        )
        
        # 可視化
        plt.figure(figsize=(15, 10))
        plt.plot(x, y, 'k-', linewidth=1, alpha=0.5, label='Original Path')
        
        # キーフレームのみを使った線形補間パス
        keyframe_x = x[optimal_keyframes]
        keyframe_y = y[optimal_keyframes]
        plt.plot(keyframe_x, keyframe_y, 'b-', linewidth=2, label='Keyframe Path')
        
        # キーフレームを散布図でプロット
        plt.scatter(keyframe_x, keyframe_y, c='red', s=100, label='Key Frames')
        
        # キーフレーム番号をアノテーション
        for i, idx in enumerate(optimal_keyframes):
            plt.annotate(f"{idx}", (keyframe_x[i], keyframe_y[i]), 
                         textcoords="offset points", xytext=(0,10), ha='center')
        
        plt.grid(True)
        plt.axis('equal')
        plt.title('Optimal Keyframes')
        plt.xlabel('PositionX')
        plt.ylabel('PositionY')
        plt.legend(fontsize=12)
        plt.savefig('optimal_keyframes.png')
        plt.show()
        
        return optimal_keyframes
        
    except FileNotFoundError:
        print(f"ファイルが見つかりません: {input_file}")
        return []

# --- 位置補間関数 ---
def linear_interpolation_position(start_idx, end_idx, keyframe_x, keyframe_y, keyframe_z, i):
    """キーフレーム間の位置を線形補間する"""
    A = np.array([keyframe_x[i], keyframe_y[i], keyframe_z[i]])
    B = np.array([keyframe_x[i+1], keyframe_y[i+1], keyframe_z[i+1]])
    
    interpolated_x = []
    interpolated_y = []
    interpolated_z = []
    
    num_frames = end_idx - start_idx
    if num_frames == 0: # 開始と終了が同じフレームの場合
        interpolated_x = [A[0]]
        interpolated_y = [A[1]]
        interpolated_z = [A[2]]
    else:
        times = np.linspace(0, 1, num_frames + 1) # 0から1まで、num_frames+1個の点を生成
        for t in times:
            P_interp = A + t * (B - A)
            interpolated_x.append(P_interp[0])
            interpolated_y.append(P_interp[1])
            interpolated_z.append(P_interp[2])
            
    return interpolated_x, interpolated_y, interpolated_z


# --- 姿勢補間関数 (SLERP) ---
def slerp_interpolation_quaternion(start_idx, end_idx, keyframe_rotations, i):
    """キーフレーム間の姿勢(Quaternion)をSLERP補間する"""
    q_start = keyframe_rotations[i]
    q_end = keyframe_rotations[i+1]
    
    key_rots = R.from_quat(np.vstack([q_start, q_end]))
    key_times = [0, end_idx - start_idx] # 各キーフレームに対応する相対時間 (フレーム数)
    
    # フレーム数が0の場合 (start == end)
    if end_idx - start_idx == 0:
        return [q_start[0]], [q_start[1]], [q_start[2]], [q_start[3]]

    slerp = Slerp(key_times, key_rots)
    
    times = np.arange(end_idx - start_idx + 1) # 補間するフレームの相対時間リスト
    interpolated_rots = slerp(times)
    
    interpolated_qx = interpolated_rots.as_quat()[:, 0]
    interpolated_qy = interpolated_rots.as_quat()[:, 1]
    interpolated_qz = interpolated_rots.as_quat()[:, 2]
    interpolated_qw = interpolated_rots.as_quat()[:, 3]
            
    return interpolated_qx.tolist(), interpolated_qy.tolist(), interpolated_qz.tolist(), interpolated_qw.tolist()

# find_optimal_keyframes 関数の後に以下の関数を追加し、メイン処理部分を修正します

def apply_keyframe_interpolation(input_file, output_file, keyframe_indices, 
                                position_interpolation_method='linear', 
                                quaternion_interpolation_method='slerp'):
    """キーフレームに基づいて補間し、結果をCSVに出力する関数"""
    try:
        # CSVファイルを読み込む
        df = pd.read_csv(input_file)
        
        # 位置データを取得
        x = df['PositionX'].values
        y = df['PositionY'].values
        z = df['PositionZ'].values
        
        # 姿勢データを取得 (X, Y, Z, W の順)
        qx = df['RotationQX'].values
        qy = df['RotationQY'].values
        qz = df['RotationQZ'].values
        qw = df['RotationQW'].values
        rotations = np.vstack([qx, qy, qz, qw]).T # (N, 4) の形状にする
        
        # 点の数を取得
        n_points = len(x)
        
        # キーフレームのインデックスを実際のデータ範囲内に収める
        keyframe_indices = [min(idx, n_points-1) for idx in keyframe_indices]
        keyframe_indices = sorted(list(set(keyframe_indices)))  # 重複を削除して昇順にソート
        
        # キーフレームの位置座標を抽出
        keyframe_x = x[keyframe_indices]
        keyframe_y = y[keyframe_indices]
        keyframe_z = z[keyframe_indices]
        
        # キーフレームの姿勢を抽出
        keyframe_rotations = rotations[keyframe_indices]
        
        # 補間結果を格納する配列を初期化
        x_interpolated = np.zeros_like(x)
        y_interpolated = np.zeros_like(y)
        z_interpolated = np.zeros_like(z)
        qx_interpolated = np.zeros_like(qx)
        qy_interpolated = np.zeros_like(qy)
        qz_interpolated = np.zeros_like(qz)
        qw_interpolated = np.zeros_like(qw)

        # 各キーフレーム間で選択された補間処理を実行
        for i in range(len(keyframe_indices) - 1):
            start_idx = keyframe_indices[i]
            end_idx = keyframe_indices[i + 1]
            num_elements = end_idx - start_idx + 1
            
            # --- 位置補間 ---
            if position_interpolation_method == 'linear':
                interp_x, interp_y, interp_z = linear_interpolation_position(start_idx, end_idx, keyframe_x, keyframe_y, keyframe_z, i)
            elif position_interpolation_method == 'projection':
                interp_x, interp_y, interp_z = projection_interpolation_position(start_idx, end_idx, keyframe_x, keyframe_y, keyframe_z, x, y, z, i)
            else:
                raise ValueError("position_interpolation_method は 'linear' または 'projection' である必要があります。")

            # 位置結果を代入
            if len(interp_x) == num_elements:
                x_interpolated[start_idx:end_idx+1] = interp_x
                y_interpolated[start_idx:end_idx+1] = interp_y
                z_interpolated[start_idx:end_idx+1] = interp_z
            else:
                print(f"警告: 位置区間 {start_idx}-{end_idx} の補間結果の長さ ({len(interp_x)}) が期待される長さ ({num_elements}) と一致しません。")
                assign_len = min(num_elements, len(interp_x))
                x_interpolated[start_idx : start_idx + assign_len] = interp_x[:assign_len]
                y_interpolated[start_idx : start_idx + assign_len] = interp_y[:assign_len]
                z_interpolated[start_idx : start_idx + assign_len] = interp_z[:assign_len]

            # --- 姿勢補間 ---
            if quaternion_interpolation_method == 'slerp':
                interp_qx, interp_qy, interp_qz, interp_qw = slerp_interpolation_quaternion(start_idx, end_idx, keyframe_rotations, i)
            else:
                raise ValueError("quaternion_interpolation_method は 'slerp' である必要があります。")

            # 姿勢結果を代入
            if len(interp_qx) == num_elements:
                qx_interpolated[start_idx:end_idx+1] = interp_qx
                qy_interpolated[start_idx:end_idx+1] = interp_qy
                qz_interpolated[start_idx:end_idx+1] = interp_qz
                qw_interpolated[start_idx:end_idx+1] = interp_qw
            else:
                print(f"警告: 姿勢区間 {start_idx}-{end_idx} の補間結果の長さ ({len(interp_qx)}) が期待される長さ ({num_elements}) と一致しません。")
                assign_len = min(num_elements, len(interp_qx))
                qx_interpolated[start_idx : start_idx + assign_len] = interp_qx[:assign_len]
                qy_interpolated[start_idx : start_idx + assign_len] = interp_qy[:assign_len]
                qz_interpolated[start_idx : start_idx + assign_len] = interp_qz[:assign_len]
                qw_interpolated[start_idx : start_idx + assign_len] = interp_qw[:assign_len]

        # --- CSVファイルの書き出し ---
        # 元のデータフレームをコピーし、補間された位置と姿勢で列を置き換える
        df_output = df.copy()
        df_output['PositionX'] = x_interpolated
        df_output['PositionY'] = y_interpolated
        df_output['PositionZ'] = z_interpolated
        df_output['RotationQX'] = qx_interpolated
        df_output['RotationQY'] = qy_interpolated
        df_output['RotationQZ'] = qz_interpolated
        df_output['RotationQW'] = qw_interpolated

        # float形式でCSVに保存 (小数点以下6桁)
        df_output.to_csv(output_file, index=False, float_format='%.6f')
        print(f"補間されたデータを {output_file} に保存しました。")
        
        return x_interpolated, y_interpolated, z_interpolated, qx_interpolated, qy_interpolated, qz_interpolated, qw_interpolated
        
    except FileNotFoundError:
        print(f"ファイルが見つかりません: {input_file}")
        return None, None, None, None, None, None, None

# メイン処理部分を修正
if __name__ == "__main__":
    # ファイルパスを設定
    input_file = "Assets/OriginalAssets/File/Exp7_Model/6.csv"
    output_file_base = "Assets/OriginalAssets/File/Exp7_Model/6_optimal"
    # 最初の初期キーフレーム
    initial_keyframes = [0, 90, 180, 270, 360, 450, 540, 630, 719]
    
    # 最適なキーフレームを見つける
    optimal_keyframes = find_optimal_keyframes(
        input_file, 
        initial_keyframes, 
        distance_threshold=0.05,
        max_recursive_depth=1
    )
    
    print("最適なキーフレーム:")
    print(optimal_keyframes)
    
    # 補間方法を設定
    position_interpolation_method = 'linear'  # 'linear' または 'projection'
    quaternion_interpolation_method = 'slerp'
    
    # 出力ファイル名を生成
    output_file = f"{output_file_base}_pos_{position_interpolation_method}_rot_{quaternion_interpolation_method}.csv"
    output_png_base = f"optimal_keyframe_pos_{position_interpolation_method}_rot_{quaternion_interpolation_method}"
    
    # 最適なキーフレームに基づいて補間し、結果をCSVに出力
    x_interp, y_interp, z_interp, qx_interp, qy_interp, qz_interp, qw_interp = apply_keyframe_interpolation(
        input_file,
        output_file,
        optimal_keyframes,
        position_interpolation_method,
        quaternion_interpolation_method
    )
    
    # 補間結果の可視化 (既存のグラフに追加)
    df = pd.read_csv(input_file)
    x_orig = df['PositionX'].values
    y_orig = df['PositionY'].values
    
    plt.figure(figsize=(15, 12))
    plt.plot(x_orig, y_orig, 'k-', linewidth=1, alpha=0.5, label='Original Model Position')
    plt.plot(x_interp, y_interp, 'r-', linewidth=2, label=f'{position_interpolation_method.capitalize()} Interpolation')
    
    # キーフレームを散布図でプロット
    keyframe_x = x_orig[optimal_keyframes]
    keyframe_y = y_orig[optimal_keyframes]
    plt.scatter(keyframe_x, keyframe_y, c='blue', s=100, label='Optimal Key Frames')
    
    # キーフレーム番号をアノテーション
    for i, idx in enumerate(optimal_keyframes):
        plt.annotate(f"{idx}", (keyframe_x[i], keyframe_y[i]), 
                     textcoords="offset points", xytext=(0,10), ha='center')
    
    plt.grid(True)
    plt.axis('equal')
    plt.title(f'Optimal Keyframes with {position_interpolation_method.capitalize()} Interpolation')
    plt.xlabel('PositionX')
    plt.ylabel('PositionY')
    plt.legend(fontsize=12)
    plt.savefig(f'{output_png_base}_comparison.png')
    plt.show()