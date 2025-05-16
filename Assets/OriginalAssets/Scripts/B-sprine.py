import numpy as np
import matplotlib.pyplot as plt
from scipy.ndimage import gaussian_filter1d
import pandas as pd
import csv

# CSVファイルを読み込む
input_file = "File/Exp1_Model/1to1.csv"
output_file = "File/Exp1_Model/smoothing200.csv"
try:
    # CSVファイルを読み込む
    df = pd.read_csv(input_file)
    
    # PositionXとPositionY列を取得
    x = df['PositionX'].values
    y = df['PositionY'].values
    z = df['PositionZ'].values
    
    # 点の数を取得
    n_points = len(x)
    t = np.linspace(0, 2*np.pi, n_points)  # 時間軸（便宜上）
    
    # 異なる標準偏差でガウシアンフィルタを適用
    sigmas = [5, 60, 200]
    x_macros = []
    y_macros = []
    z_macros = []
    
    for sigma in sigmas:
        x_macro = gaussian_filter1d(x, sigma)
        y_macro = gaussian_filter1d(y, sigma)
        z_macro = gaussian_filter1d(z, sigma)
        x_macros.append(x_macro)
        y_macros.append(y_macro)
        z_macros.append(z_macro)
    
    # # 静的可視化：異なる標準偏差での比較
    # plt.figure(figsize=(15, 12))
    
    # # 元のモデル曲線
    # plt.plot(x, y, 'k-', linewidth=2, label='model (元のデータ)')
    # plt.scatter(x, y, c='black', s=40, alpha=0.4)
    
    # # 異なる標準偏差でのmacro曲線
    # colors = ['r', 'g', 'b']
    # labels = [f'macro (σ={sigma})' for sigma in sigmas]
    
    # for i, (x_macro, y_macro) in enumerate(zip(x_macros, y_macros)):
    #     plt.plot(x_macro, y_macro, f'{colors[i]}-', linewidth=2, label=labels[i])
    #     plt.scatter(x_macro, y_macro, c=colors[i], s=60, alpha=0.6)
    
    # plt.grid(True)
    # plt.axis('equal')
    # plt.title('実験データに対する異なる標準偏差でのガウス平滑化比較')
    # plt.xlabel('PositionX')
    # plt.ylabel('PositionY')
    # plt.legend(fontsize=12)
    # plt.savefig('experiment_gaussian_comparison_static.png')
    
    # # 時間に対する位置の比較プロット（x座標）
    # plt.figure(figsize=(15, 6))
    # plt.plot(range(n_points), x, 'k-', linewidth=2, label='model')
    
    # for i, x_macro in enumerate(x_macros):
    #     plt.plot(range(n_points), x_macro, f'{colors[i]}-', linewidth=2, label=labels[i])
    
    # plt.title('X座標の時間変化（異なる標準偏差での比較）')
    # plt.xlabel('フレーム')
    # plt.ylabel('PositionX')
    # plt.legend(fontsize=12)
    # plt.grid(True)
    # plt.savefig('experiment_x_gaussian_comparison.png')
    
    # # 時間に対する位置の比較プロット（y座標）
    # plt.figure(figsize=(15, 6))
    # plt.plot(range(n_points), y, 'k-', linewidth=2, label='model')
    
    # for i, y_macro in enumerate(y_macros):
    #     plt.plot(range(n_points), y_macro, f'{colors[i]}-', linewidth=2, label=labels[i])
    
    # plt.title('Y座標の時間変化（異なる標準偏差での比較）')
    # plt.xlabel('フレーム')
    # plt.ylabel('PositionY')
    # plt.legend(fontsize=12)
    # plt.grid(True)
    # plt.savefig('experiment_y_gaussian_comparison.png')
    
    
    # # 軸の範囲を設定（すべてのプロットで同じ範囲にする）
    # x_min = min([min(x)] + [min(x_m) for x_m in x_macros]) * 1.1
    # x_max = max([max(x)] + [max(x_m) for x_m in x_macros]) * 1.1
    # y_min = min([min(y)] + [min(y_m) for y_m in y_macros]) * 1.1
    # y_max = max([max(y)] + [max(y_m) for y_m in y_macros]) * 1.1
    
    new_data = []
    for i in range(len(x)):
        new_data.append([x_macros[2][i], y_macros[2][i], z_macros[2][i]])

    # CSVファイルを読み込み、特定の列を置き換えて新しいファイルに書き出す
    def replace_columns_and_write(input_file, output_file, new_data):
        rows = []
        
        # 元のCSVファイルを読み込む
        with open(input_file, 'r', newline='', encoding='utf-8') as csvfile:
            reader = csv.reader(csvfile)
            rows = list(reader)
        
        rows[0][2:5] = ['PositionX', 'PositionY', 'PositionZ']
        # 各行の3列目から5列目（インデックス2から4）を新しいデータで置き換える
        for i in range(min(len(rows), len(new_data))):
            rows[i+1][2:5] = new_data[i]
        
        # 新しいCSVファイルに書き出す
        with open(output_file, 'w', newline='', encoding='utf-8') as csvfile:
            writer = csv.writer(csvfile)
            writer.writerows(rows)
        
        print(f"{input_file}の内容を修正し、{output_file}に保存しました。")

    # 関数を実行
    replace_columns_and_write(input_file, output_file, new_data)
    
    
    plt.tight_layout()
    plt.show()

except FileNotFoundError:
    print(f"ファイルが見つかりません: {input_file}")
    
    # ファイルが見つからない場合は代替のデータを作成
    print("代替の抽象的なデータを使用します。")
    
    # 100個の点を生成
    n_points = 100
    t = np.linspace(0, 2*np.pi, n_points)
    
    # 時系列データの生成（ランダムな動き）
    np.random.seed(42)
    x = np.cumsum(np.random.normal(0, 0.1, n_points))
    y = np.cumsum(np.random.normal(0, 0.1, n_points))
    
