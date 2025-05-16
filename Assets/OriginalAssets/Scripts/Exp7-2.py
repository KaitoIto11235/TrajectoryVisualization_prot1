import numpy as np
import pandas as pd
import pylab as pl
import scipy.interpolate as it
import seaborn as sns
import random
import matplotlib.pyplot as plt
import os


# --- 新しい関数: 90フレームごとの区間ベクトルの距離 ---
def calculate_keyframe_vector_errors(df_model, df_test, frame_interval=90, max_frame=719):
    """
    time列に有効な数値を持つ行を上から数え、その行番号をフレーム番号として扱い、
    指定されたフレーム間隔で区切り、各区間の始点と終点の差分ベクトルを計算し、
    見本データとテストデータのベクトル間の内積を計算する関数。

    Parameters:
    -----------
    df_model : DataFrame
        見本の時系列データ (PositionX, PositionY, PositionZ を含む、インデックスはリセット済み)
    df_test : DataFrame
        学習者の時系列データ (PositionX, PositionY, PositionZ を含む、インデックスはリセット済み)
    frame_interval : int, optional
        区間のフレーム数 (default: 90)
    max_frame : int, optional
        計算対象の最大フレーム番号 (default: 720)

    Returns:
    --------
    list of dict
        各区間の内積計算結果を格納したリスト
        [{'StartFrame': int, 'EndFrame': int, 'VecDistance': float}, ...]
    """
    results = []
    position_cols = ['PositionX', 'PositionY', 'PositionZ']

    for start_frame in range(0, max_frame, frame_interval):
        end_frame = start_frame + frame_interval-1

        # フレームインデックス（リセット後の行番号）が存在するか確認
        if start_frame < len(df_model) and end_frame < len(df_model) and \
           start_frame < len(df_test) and end_frame < len(df_test):

            # 見本のベクトル計算 (ilocを使用して行番号でアクセス)
            model_start_pos = df_model.iloc[start_frame][position_cols].values.astype(float)
            model_end_pos = df_model.iloc[end_frame][position_cols].values.astype(float)
            model_vector = model_end_pos - model_start_pos

            # テストデータのベクトル計算 (ilocを使用して行番号でアクセス)
            test_start_pos = df_test.iloc[start_frame][position_cols].values.astype(float)
            test_end_pos = df_test.iloc[end_frame][position_cols].values.astype(float)
            test_vector = test_end_pos - test_start_pos

            # 内積計算
            # ベクトルを正規化
            model_vector_normalized = model_vector / np.linalg.norm(model_vector) if np.linalg.norm(model_vector) > 0 else model_vector
            test_vector_normalized = test_vector / np.linalg.norm(test_vector) if np.linalg.norm(test_vector) > 0 else test_vector

            # 正規化されたベクトル間の内積計算
            dot_product = np.dot(model_vector_normalized, test_vector_normalized)

            results.append({
                'StartFrame': start_frame,
                'EndFrame': end_frame,
                'VecDistance': dot_product
            })
        else:
            # 必要なフレーム番号（行）が存在しない場合
            print(f"    Warning: Frame index {start_frame} or {end_frame} out of bounds after filtering. Skipping segment {start_frame}-{end_frame}.")
            results.append({
                'StartFrame': start_frame,
                'EndFrame': end_frame,
                'VecDistance': np.nan # 存在しない場合はNaN
            })

    return results


# --- ここからメイン処理 ---

# 実験ごとのテストファイル数を定義
max_tests_per_exp = {
    1: 4,
    2: 4,
    3: 6,
    4: 4,
    5: 3,
    6: 3
}

# 結果を格納するリスト 
segment_dot_product_results = [] # ★ 新しい結果リストを追加

segment_dot_product_output_csv = 'Assets/OriginalAssets/File/Exp7_segment_dot_product_results.csv' # ★ 新しい出力パスを追加

# 実験 1 から 6 までループ
for exp_num in range(1, 7):
    print(f"\n===== Processing Experiment {exp_num} =====")
    
    # モデルファイルパスを生成
    model_file = f'Assets/OriginalAssets/File/Exp7_Model/{exp_num}_pos_linear_rot_slerp.csv'
    # テストファイルのベースパスを生成
    base_path = f'Assets/OriginalAssets/File/Exp7_{exp_num}/'
    
    # 見本データを読み込む
    try:
        df_model = pd.read_csv(model_file)
        # time列を数値型に変換（エラー時はNaN）
        df_model['time'] = pd.to_numeric(df_model['time'], errors='coerce')
        # NaNになった行を削除（または他の処理）
        df_model.dropna(subset=['time'], inplace=True)
        # ★ インデックスをリセットして、有効な行が0から始まるようにする
        df_model.reset_index(drop=True, inplace=True)
        # 必要であればtime列を整数型に変換 -> リセットしたので不要かも
        # df_model['time'] = df_model['time'].astype(int)
    except FileNotFoundError:
        print(f"  Model file not found: {model_file}. Skipping experiment {exp_num}.")
        continue # 次の実験へ

    # その実験の最大テスト数を取得
    max_test_num = max_tests_per_exp.get(exp_num, 0)
    if max_test_num == 0:
        print(f"  Max test number not defined for experiment {exp_num}. Skipping.")
        continue

    # Te1からTeNまでのファイルを処理
    for test_num in range(1, max_test_num + 1):
        test_file_name = f'_Te{test_num}.csv'
        test_file_path = base_path + test_file_name
        
        print(f"\n-- Processing Test File: {test_file_name} --")
        
        try:
            # テストデータを読み込む
            df_test_file = pd.read_csv(test_file_path)
            # time列を数値型に変換（エラー時はNaN）
            df_test_file['time'] = pd.to_numeric(df_test_file['time'], errors='coerce')
            # NaNになった行を削除
            df_test_file.dropna(subset=['time'], inplace=True)
            # ★ インデックスをリセットして、有効な行が0から始まるようにする
            df_test_file.reset_index(drop=True, inplace=True)
            # time列を整数型に変換 -> リセットしたので不要かも
            # df_test_file['time'] = df_test_file['time'].astype(int)
            #df_t = pd.read_csv(test_file_path, dtype=str) # time列の読み込み用に残す場合 -> 不要に
        except FileNotFoundError:
            print(f"  Test file not found: {test_file_path}. Skipping this test.")
            continue # 次のテストファイルへ

        # time列でのフィルタリングとインデックスリセットは読み込み時に実施済み
        df_test = df_test_file

        
        # --- 3. 区間ベクトル内積の計算 (新規) ---
        print("  Calculating Segment Vector Dot Products...")
        try:
            segment_results = calculate_keyframe_vector_errors(df_model, df_test)
            for result in segment_results:
                segment_dot_product_results.append({
                    'ExpNum': exp_num,
                    'TestNum': test_num,
                    'StartFrame': result['StartFrame'],
                    'EndFrame': result['EndFrame'],
                    'VecDistance': result['VecDistance']
                })
        except Exception as e:
            print(f"    Error (Segment Dot Product): {e}")
            # エラーが発生した場合、そのテストファイルの区間結果は記録しないか、
            # エラーを示す値を記録するかを選択できます。ここでは記録しません。

# --- 全ての処理が終わったらDataFrameに変換してCSVに出力 ---

def save_results_to_csv(results_list, output_path):
    if results_list:
        results_df = pd.DataFrame(results_list)
        try:
            output_dir = os.path.dirname(output_path)
            if output_dir and not os.path.exists(output_dir): # output_dirが空でないことも確認
                os.makedirs(output_dir)
                print(f"Created directory: {output_dir}")
            results_df.to_csv(output_path, index=False, encoding='utf-8-sig')
            print(f"\nResults successfully saved to {output_path}")
        except Exception as e:
            print(f"\nError saving results to {output_path}: {e}")
    else:
        print(f"\nNo results were generated for {output_path}.")

# 各結果を保存
save_results_to_csv(segment_dot_product_results, segment_dot_product_output_csv) # ★ 新しい結果を保存


# 既存の plt.figure, plt.tight_layout, plt.show は不要
# plt.figure(figsize=(12, 15))
# plt.tight_layout()  # レイアウトを調整
# plt.show()

