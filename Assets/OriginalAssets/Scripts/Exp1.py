import numpy as np
import pandas as pd
import pylab as pl
import scipy.interpolate as it
import seaborn as sns
import random
import matplotlib.pyplot as plt

# Auxiliary functions
def get_mirror(s, ws):
    
    """
    Performs a signal windowing based on a double inversion from the start and end segments.
    :param s: (array-like)
            the input-signal.
    :param ws: (integer)
            window size.
    :return:
    """

    return np.r_[2 * s[0] - s[ws:0:-1], s, 2 * s[-1] - s[-2:-ws - 2:-1]]


def normalize_signal(s):
    """
    Normalizes a given signal by subtracting the mean and dividing by the standard deviation.
    :param s: (array_like)
            The input signal.
    :return:
            The normalized input signal.
    """
    return (s - np.mean(s)) / np.std(s)

def sliding_dist(Axw, Ayw, Azw, Bxw, Byw, Bzw, dAxw, dAyw, dAzw, dBxw, dByw, dBzw, a, win):
    dw = np.sqrt(np.sum(((dAxw - dBxw) * win) ** 2.) + np.sum(((dAyw - dByw) * win) ** 2.) + np.sum(((dAzw - dBzw) * win) ** 2.))
    w = np.sqrt(np.sum(((Axw - Bxw) * win) ** 2.) + np.sum(((Ayw - Byw) * win) ** 2.) + np.sum(((Azw - Bzw) * win) ** 2.))
    return (1 - a) * dw + a * w

# def _traceback(D):
#     i, j = np.array(D.shape) - 2
#     p, q = [i], [j]
#     while (i > 0) or (j > 0):
#         tb = np.argmin((D[i + 1, j + 1], D[i, j + 1], D[i + 1, j]))
#         if tb == 0:
#             i -= 1
#             j -= 1
#         elif tb == 1:
#             i -= 1
#         else:  # (tb == 2):
#             j -= 1
#         p.insert(0, i)
#         q.insert(0, j)
        
#     return np.array(p), np.array(q)

def _traceback(D):
    i, j = np.array(D.shape) - 2
    p, q = [i], [j]
    while (i > 0) or (j > 0):
        tb = np.argmin((D[i, j], D[i, j + 1], D[i + 1, j]))
        if tb == 0:
            i -= 1
            j -= 1
        elif tb == 1:
            i -= 1
        else:  # (tb == 2):
            j -= 1
        p.insert(0, i)
        q.insert(0, j)
        
    return np.array(p), np.array(q)


def align_sequences(ref, s, path):
    """
    This functions aligns two time-series. The alignment is performed
    for a given reference signal and a vector containing the alignment.
    :param ref: (array-like)
            The reference signal.
    :param s: (array-like)
            The signal to be aligned.
    :param path: (ndarray)
            A rank 2 array containing the optimal warping path between the two signals.
    :return:
    """
    nt = np.linspace(0, len(ref) - 1, len(ref))
    ns = it.interp1d(path[0], s[path[1]])(nt)

    return ns


# Visualization
def plot_alignment(ref_signal, estimated_signal, path, **kwargs):
    """
    This functions plots the resulted alignment of two sequences given the path
    calculated by the Dynamic Time Warping algorithm.

    :param ref_signal: (array-like)
                     The reference sequence.
    :param estimated_signal: (array-like)
                     The estimated sequence.
    :param path: (array-like)
                     A 2D array congaing the path resulted from the algorithm
    :param \**kwargs:
        See below:

        * *offset* (``double``) --
            The offset used to move the reference signal to an upper position for
            visualization purposes.
            (default: ``2``)

        * *linewidths* (``list``) --
            A list containing the linewidth for the reference, estimated and connection
            plots, respectively.
            (default: ``[3, 3, 0.5]``)

        * *step* (``int``) --
            The step for
          (default: ``2``)

        * *colors* (``list``) --
          A list containing the colors for the reference, estimated and connection
          plots, respectively.
          (default: ``[sns.color_palette()[0], sns.color_palette()[1], 'k', 'k', 'k', 'k']``)
    """

    step = kwargs.get('step', 2)
    offset = kwargs.get('offset', 2)
    linewidths = kwargs.get('linewidths', [3, 3, 0.5])
    colors = kwargs.get('colors', [sns.color_palette()[0], sns.color_palette()[1], 'k', 'k', 'k', 'k'])

    # 上下に分割して実際の値を表示するために、Y軸の範囲を設定
    ref_min = min(np.nanmin(ref_signal), np.nanmin(estimated_signal))
    ref_max = max(np.nanmax(ref_signal), np.nanmax(estimated_signal))
    est_min, est_max = ref_min, ref_max
    # ref_min, ref_max = -1, 1
    # est_min, est_max = -1, 1
    
    # Y軸の範囲を計算（余白を含む）- ゼロ除算を防ぐ
    range_ref = ref_max - ref_min
    #range_ref = 2
    range_est = est_max - est_min
    #range_est = 2
    
    # 範囲がゼロまたは無効な場合のデフォルト値
    if np.isnan(range_ref) or range_ref <= 1e-10:
        range_ref = 1.0
    if np.isnan(range_est) or range_est <= 1e-10:
        range_est = 1.0
    
    # 中心線の位置を0とする
    center_line = 0
    
    # 参照信号を上半分に配置 - 値が大きいほど上に表示
    ref_signal_shifted = center_line + (ref_signal - ref_min) / range_ref * range_ref + range_ref * 0.1
    
    # 推定信号を下半分に配置 - 値が大きいほど下から上に表示（反転しない）
    est_signal_shifted = center_line - range_est * 1.1 + (estimated_signal - est_min) / range_est * range_est

    # 実際のプロット
    pl.axhline(y=center_line, color='gray', linestyle='--', alpha=0.5)  # 中心線を表示
    
    # 実際の値を表示
    pl.plot(ref_signal_shifted, color=sns.color_palette()[1], lw=linewidths[0], label='model')
    pl.plot(est_signal_shifted, color=sns.color_palette()[0], lw=linewidths[1], label='user')
    pl.legend(fontsize=5)
    
    # Y軸ラベルを両側に表示
    ax = pl.gca()
    ax2 = ax.twinx()
    
    # 元のY軸範囲に戻すための変換関数
    def ref_to_orig(y):
        return (y - center_line - range_ref * 0.1) * range_ref / range_ref + ref_min
    
    def est_to_orig(y):
        return ((y - center_line + range_est * 1.1) * range_est / range_est) + est_min
    
    # Y軸の設定 - NaNやInfを防ぐ
    y_min = center_line - range_est * 1.5
    y_max = center_line + range_ref * 1.5
    
    # NaNやInfをチェック
    if np.isnan(y_min) or np.isinf(y_min):
        y_min = -1.0
    if np.isnan(y_max) or np.isinf(y_max):
        y_max = 1.0
        
    ax.set_ylim(y_min, y_max)
    ax2.set_ylim(y_min, y_max)
    
    try:
        # 元の範囲に対応するY軸目盛りを設定
        ref_ticks = np.linspace(ref_min, ref_max, 5)
        est_ticks = np.linspace(est_min, est_max, 5)
        
        # 目盛り位置の計算 - userも値が大きいほど上になるよう修正
        ref_tick_pos = [center_line + (t - ref_min) / range_ref * range_ref + range_ref * 0.1 for t in ref_ticks]
        est_tick_pos = [center_line - range_est * 1.1 + (t - est_min) / range_est * range_est for t in est_ticks]
        
        # 目盛りにNaNが含まれていないか確認
        if not np.any(np.isnan(ref_tick_pos)) and not np.any(np.isnan(est_tick_pos)):
            # 左側のY軸はuserの値
            ax.set_yticks(est_tick_pos)
            ax.set_yticklabels([f"{t:.2f}" for t in est_ticks])
            ax.set_ylabel('User', color=sns.color_palette()[0])
            ax.tick_params(axis='y', colors=sns.color_palette()[0])
            
            # 右側のY軸はmodelの値
            ax2.set_yticks(ref_tick_pos)
            ax2.set_yticklabels([f"{t:.2f}" for t in ref_ticks])
            ax2.set_ylabel('Model', color=sns.color_palette()[1])
            ax2.tick_params(axis='y', colors=sns.color_palette()[1])
    except Exception as e:
        print(f"Y軸目盛りの設定でエラーが発生しました: {e}")
        # デフォルトの目盛りを使用
    
    # DTWパスの描画
    try:
        for i in range(len(path[0]))[step * 0::step * 4]:
            if path[0][i] < len(ref_signal_shifted) and path[1][i] < len(est_signal_shifted):
                pl.plot([path[0][i], path[1][i]], 
                        [ref_signal_shifted[path[0][i]], est_signal_shifted[path[1][i]]], 
                        color=colors[2], lw=linewidths[2])
        for i in range(len(path[0]))[step * 1::step * 4]:
            if path[0][i] < len(ref_signal_shifted) and path[1][i] < len(est_signal_shifted):
                pl.plot([path[0][i], path[1][i]], 
                        [ref_signal_shifted[path[0][i]], est_signal_shifted[path[1][i]]], 
                        color=colors[3], lw=linewidths[2])
        for i in range(len(path[0]))[step * 2::step * 4]:
            if path[0][i] < len(ref_signal_shifted) and path[1][i] < len(est_signal_shifted):
                pl.plot([path[0][i], path[1][i]], 
                        [ref_signal_shifted[path[0][i]], est_signal_shifted[path[1][i]]], 
                        color=colors[4], lw=linewidths[2])
        for i in range(len(path[0]))[step * 3::step * 4]:
            if path[0][i] < len(ref_signal_shifted) and path[1][i] < len(est_signal_shifted):
                pl.plot([path[0][i], path[1][i]], 
                        [ref_signal_shifted[path[0][i]], est_signal_shifted[path[1][i]]], 
                        color=colors[5], lw=linewidths[2])
    except Exception as e:
        print(f"DTWパスの描画でエラーが発生しました: {e}")

    
def plot_costmatrix(matrix, path):
    """
    This functions overlays the optimal warping path and the cost matrices
    :param matrix: (ndarray-like)
                The cost matrix (local cost or accumulated)
    :param path:   (ndarray-like)
                The optimal warping path
    :return: (void)
                Plots the optimal warping path with an overlay of the cost matrix.
    """
    pl.imshow(matrix.T, cmap='viridis', origin='lower', interpolation='None')
    pl.colorbar()
    #pl.plot(path[0], path[1], 'w.-')
    pl.xlim((-0.5, matrix.shape[0] - 0.5))
    pl.ylim((-0.5, matrix.shape[1] - 0.5))
    
    
def dtw_sw(Ax, Ay, Az, Bx, By, Bz, winlen, alpha=0.5, **kwargs):
    """
    Computes Dynamic Time Warping (DTW) of two time series.
    :param x: (array_like)
            The reference signal.
    :param y: (array_like)
            The estimated signal.
    :param winlen: (int)
            The sliding window length
    :param alpha: (float)
            A factor between 0 and 1 which weights the amplitude and derivative contributions.
            A higher value will favor amplitude and a lower value will favor the first derivative.

    :param \**kwargs:
        See below:

        * *do_sign_norm* (``bool``) --
          If ``True`` the signals will be normalized before computing the DTW,
          (default: ``False``)

        * *do_dist_norm* (``bool``) --
          If ``True`` the DTW distance will be normalized by dividing the summation of the path dimension.
          (default: ``True``)

        * *window* (``String``) --
          Selects the global window constrains. Available options are ``None`` and ``sakoe-chiba``.
          (default: ``None``)

        * *factor* (``Float``) --
          Selects the global constrain factor.
          (default: ``min(xl, yl) * .50``)


    :return:
           d: (float)
            The SW-DTW distance.
           C: (array_like)
            The local cost matrix.
           ac: (array_like)
            The accumulated cost matrix.
           path (array_like)
            The optimal warping path between the two sequences.
    """
    Axl, Bxl = len(Ax), len(Bx)

    do_sign_norm = kwargs.get('normalize', False)
    do_dist_norm = kwargs.get('dist_norm', True)
    window = kwargs.get('window', None)
    factor = kwargs.get('factor', np.min((Axl, Bxl)) * .50)

    if do_sign_norm:
        Ax, Ay, Az, Bx, By, Bz= normalize_signal(Ax), normalize_signal(Ay), normalize_signal(Az), normalize_signal(Bx), normalize_signal(By), normalize_signal(Bz)

    ac = np.zeros((Axl + 1, Bxl + 1))
    ac[0, 1:] = np.inf
    ac[1:, 0] = np.inf
    tmp_ac = ac[1:, 1:]

    nAx = get_mirror(Ax, winlen)
    nAy = get_mirror(Ay, winlen)
    nAz = get_mirror(Az, winlen)
    nBx = get_mirror(Bx, winlen)
    nBy = get_mirror(By, winlen)
    nBz = get_mirror(Bz, winlen)

    dnAx = np.diff(nAx, axis = 0)
    dnAy = np.diff(nAy, axis = 0)
    dnAz = np.diff(nAz, axis = 0)
    dnBx = np.diff(nBx, axis = 0)
    dnBy = np.diff(nBy, axis = 0)
    dnBz = np.diff(nBz, axis = 0)

    nAx = nAx[:-1]
    nAy = nAy[:-1]
    nAz = nAz[:-1]
    nBx = nBx[:-1]
    nBy = nBy[:-1]
    nBz = nBz[:-1]

    # Workaround to deal with even window sizes
    if winlen % 2 == 0:
        winlen -= 1

    swindow = np.hamming(winlen)
    swindow = swindow / np.sum(swindow)

    for i in range(Axl):
        for j in range(Bxl):
            pad_i, pad_j = i + winlen, j + winlen
            # No window selected
            if window is None:
                tmp_ac[i, j] = sliding_dist(nAx[pad_i - (winlen // 2):pad_i + (winlen // 2) + 1],
                                        nAy[pad_i - (winlen // 2):pad_i + (winlen // 2) + 1],
                                        nAz[pad_i - (winlen // 2):pad_i + (winlen // 2) + 1],
                                        nBx[pad_j - (winlen // 2):pad_j + (winlen // 2) + 1],
                                        nBy[pad_j - (winlen // 2):pad_j + (winlen // 2) + 1],
                                        nBz[pad_j - (winlen // 2):pad_j + (winlen // 2) + 1],
                                        dnAx[pad_i - (winlen // 2):pad_i + (winlen // 2) + 1],
                                        dnAy[pad_i - (winlen // 2):pad_i + (winlen // 2) + 1],
                                        dnAz[pad_i - (winlen // 2):pad_i + (winlen // 2) + 1],
                                        dnBx[pad_j - (winlen // 2):pad_j + (winlen // 2) + 1],
                                        dnBy[pad_j - (winlen // 2):pad_j + (winlen // 2) + 1],
                                        dnBz[pad_j - (winlen // 2):pad_j + (winlen // 2) + 1], alpha, swindow)

            # Sakoe-Chiba band
            elif window == 'sakoe-chiba':
                if abs(i - j) < factor:
                    tmp_ac[i, j] = sliding_dist(nAx[pad_i - (winlen // 2):pad_i + (winlen // 2) + 1],
                                            nAy[pad_i - (winlen // 2):pad_i + (winlen // 2) + 1],
                                            nAz[pad_i - (winlen // 2):pad_i + (winlen // 2) + 1],
                                            nBx[pad_j - (winlen // 2):pad_j + (winlen // 2) + 1],
                                            nBy[pad_j - (winlen // 2):pad_j + (winlen // 2) + 1],
                                            nBz[pad_j - (winlen // 2):pad_j + (winlen // 2) + 1],
                                            dnAx[pad_i - (winlen // 2):pad_i + (winlen // 2) + 1],
                                            dnAy[pad_i - (winlen // 2):pad_i + (winlen // 2) + 1],
                                            dnAz[pad_i - (winlen // 2):pad_i + (winlen // 2) + 1],
                                            dnBx[pad_j - (winlen // 2):pad_j + (winlen // 2) + 1],
                                            dnBy[pad_j - (winlen // 2):pad_j + (winlen // 2) + 1],
                                            dnBz[pad_j - (winlen // 2):pad_j + (winlen // 2) + 1], alpha, swindow)
                else:
                    tmp_ac[i, j] = np.inf

            # As last resource, the complete window is calculated
            else:
                tmp_ac[i, j] = sliding_dist(nAx[pad_i - (winlen // 2):pad_i + (winlen // 2) + 1],
                                        nBx[pad_j - (winlen // 2):pad_j + (winlen // 2) + 1],
                                        dnAx[pad_i - (winlen // 2):pad_i + (winlen // 2) + 1],
                                        dnBx[pad_j - (winlen // 2):pad_j + (winlen // 2) + 1], alpha, swindow)
    c = tmp_ac.copy()

    for i in range(Axl):
        for j in range(Bxl):
            tmp_ac[i, j] += min([ac[i, j], ac[i, j + 1], ac[i + 1, j]]) #ac[i, j] == tmp_ac[i-1, j-1]のため。

    path = _traceback(ac)

    if do_dist_norm:
        d = ac[-1, -1] / np.sum(np.shape(path))
    else:
        d = ac[-1, -1]

    return d, c, ac, path

def dtwDistance(Ax, Ay, Az, Bx, By, Bz, pathA, pathB):
    i = 0
    dist = 0
    length = len(pathA) - 1  # なぜかlen(pathA)がインデックスの時、Bx,By,Bzがnanを返すため、対処。
    while i < length:
        dist = dist + np.sqrt((Ax[pathA[i]] - Bx[pathB[i]]) ** 2 + (Ay[pathA[i]] - By[pathB[i]]) ** 2 + (Az[pathA[i]] - Bz[pathB[i]]) ** 2)
        i = i + 1
    dist = dist / np.sum(np.shape([pathA, pathB]))
    
    return dist

def eucDistance(Ax, Ay, Az, Bx, By, Bz):
    i=0
    dist = 0
    length = len(Ax)-1
    if(len(Ax) < len(Bx)):
        length = len(Ax)-1
    else:
        length = len(Bx)-1
    while i < length:
        dist = dist + np.sqrt((Ax[i] - Bx[i]) ** 2 + (Ay[i] - By[i]) ** 2 + (Az[i] - Bz[i]) ** 2)
        i = i + 1
    dist = dist / length
    
    return dist

def dtwQuaternion(Ax, Ay, Az, Aw, Bx, By, Bz, Bw, pathA, pathB):
    i = 0
    dist = 0
    length = len(pathA) - 1  # なぜかlen(pathA)がインデックスの時、Bx,By,Bzがnanを返すため、対処。
    while i < length:
        dist = dist + abs((Ax[pathA[i]] * Bx[pathB[i]]) + (Ay[pathA[i]] * By[pathB[i]]) + (Az[pathA[i]] * Bz[pathB[i]]) + (Aw[pathA[i]] * Bw[pathB[i]]))
        i = i + 1
    dist = dist / np.sum(np.shape([pathA, pathB]))
    
    return dist

def eucQuaternion(Ax, Ay, Az, Aw, Bx, By, Bz, Bw):
    i=0
    dist = 0
    length = len(Ax)-1
    if(len(Ax) < len(Bx)):
        length = len(Ax)-1
    else:
        length = len(Bx)-1
    while i < length:
        dist = dist + abs((Ax[i] * Bx[i]) + (Ay[i] * By[i]) + (Az[i] * Bz[i]) + (Aw[i] * Bw[i]))
        i = i + 1
    dist = dist / length
    
    return dist

# 差分ベクトルが0の場合を処理する補助関数
def compute_non_zero_diffs(positions):
    # 初期の差分を計算
    diffs = positions.copy()  # 入力をそのままコピー
    
    # 入力が既に差分なら処理を続行
    result = []
    i = 0
    while i < len(diffs):
        # 現在の差分が0ベクトルかチェック
        if np.allclose(diffs[i], 0):
            # 次の非ゼロ点を探す
            next_non_zero = i + 1
            while next_non_zero < len(diffs) and np.allclose(diffs[next_non_zero], 0):
                next_non_zero += 1
            
            # 非ゼロ点が見つかった場合
            if next_non_zero < len(diffs):
                # 0でない値をそのまま使用
                result.append(diffs[next_non_zero])
                # インデックスを更新
                i = next_non_zero + 1
            else:
                # 残りすべて0の場合、スキップ
                break
        else:
            # 通常の差分をそのまま使用
            result.append(diffs[i])
            i += 1
    
    # 結果が空の場合は空の配列を返す
    if len(result) == 0:
        return np.array([])
            
    return np.array(result)

def calculate_vector_dot_product(df_model, df_test, mode='dtw_path', dtw_path=None, call = 1):
    """
    見本と学習者の位置ベクトルの内積を計算する関数
    
    Parameters:
    -----------
    df_model : DataFrame
        見本の時系列データ
    df_test : DataFrame
        学習者の時系列データ
    mode : str
        'dtw_path': 与えられたDTWパスを使用 (正規化あり)
        'dtw_calc': 正規化前のベクトルでDTWを計算して使用 (正規化あり)
        'same_time': 同じ時間フレームで計算 (正規化あり)
        'relative_time': 長い時系列を圧縮して同じ時間フレームで計算 (正規化あり)
        'raw_dot_product_same_time': 同じ時間フレームで計算 (正規化なし)
    dtw_path : tuple, optional
        DTWパス（mode='dtw_path'の場合に必要）
    call : int
        プロットの位置を指定するためのパラメータ
    
    Returns:
    --------
    float
        内積の平均値
    """
    # 見本のベクトルを計算
    model_diffs = np.array([
        df_model["PositionX"].diff().values[1:],
        df_model["PositionY"].diff().values[1:],
        df_model["PositionZ"].diff().values[1:]
    ]).T
    
    # 学習者のベクトルを計算
    test_diffs = np.array([
        df_test["PositionX"].diff().values[1:],
        df_test["PositionY"].diff().values[1:],
        df_test["PositionZ"].diff().values[1:]
    ]).T
    
    # 非ゼロ差分ベクトルを計算
    model_vectors = compute_non_zero_diffs(model_diffs)
    test_vectors = compute_non_zero_diffs(test_diffs)

    # 有効なベクトルが存在しない場合は0を返す
    if len(model_vectors) == 0 or len(test_vectors) == 0:
        return 0.0

    if mode == 'dtw_path':
        if dtw_path is None:
            raise ValueError("dtw_path must be provided when mode is 'dtw_path'")
        # 与えられたDTWパスに沿って内積を計算
        dot_products = []
        path0 = dtw_path[0]
        path1 = dtw_path[1]
        for i in range(len(path0)-1):
            model_idx = path0[i]
            test_idx = path1[i]
            if model_idx < len(model_vectors) and test_idx < len(test_vectors):
                # 内積を計算
                dot_product = np.dot(model_vectors[model_idx], test_vectors[test_idx])
                # ベクトルの長さで正規化
                model_norm = np.linalg.norm(model_vectors[model_idx])
                test_norm = np.linalg.norm(test_vectors[test_idx])
                if model_norm > 0 and test_norm > 0:
                    dot_products.append(dot_product / (model_norm * test_norm))
        # dtw_pathは引数で与えられたものを使用

    elif mode == 'dtw_calc':
        # 正規化前のベクトルを用いてDTWを計算
        dtw_result = dtw_sw(
            model_vectors[:, 0], model_vectors[:, 1], model_vectors[:, 2],
            test_vectors[:, 0], test_vectors[:, 1], test_vectors[:, 2],
            12, 0.5, window='sakoe-chiba', factor=300
        )
        
        dtw_path = dtw_result[3]
        path0 = dtw_path[0]
        path1 = dtw_path[1]
        
        # DTWパスに沿って内積を計算
        dot_products = []
        for i in range(len(path0)-1):
            model_idx = path0[i]
            test_idx = path1[i]
            if model_idx < len(model_vectors) and test_idx < len(test_vectors):
                # 内積を計算
                dot_product = np.dot(model_vectors[model_idx], test_vectors[test_idx])
                # ベクトルの長さで正規化
                model_norm = np.linalg.norm(model_vectors[model_idx])
                test_norm = np.linalg.norm(test_vectors[test_idx])
                if model_norm > 0 and test_norm > 0:
                    dot_products.append(dot_product / (model_norm * test_norm))
    
    elif mode == 'same_time':
        # 同じ時間フレームで内積を計算
        min_length = min(len(model_vectors), len(test_vectors))
        dot_products = []
        for i in range(min_length):
            # 内積を計算
            dot_product = np.dot(model_vectors[i], test_vectors[i])
            # ベクトルの長さで正規化
            model_norm = np.linalg.norm(model_vectors[i])
            test_norm = np.linalg.norm(test_vectors[i])
            if model_norm > 0 and test_norm > 0:
                dot_products.append(dot_product / (model_norm * test_norm))
        # 同時刻のDTWパスを作成
        path0 = list(range(min_length))
        path1 = list(range(min_length))
        dtw_path = (path0, path1)
        
    
    elif mode == 'relative_time':
        # 長い時系列を圧縮して同じ時間フレームで計算
        if len(model_vectors) > len(test_vectors):
            # model_vectorsを圧縮
            indices = np.linspace(0, len(model_vectors)-1, len(test_vectors)).astype(int)
            model_vectors_compressed = model_vectors[indices]
            test_vectors_compressed = test_vectors # 圧縮しない方を保持
            min_length = len(test_vectors)
            dot_products = []
            for i in range(min_length):
                # 内積を計算
                dot_product = np.dot(model_vectors_compressed[i], test_vectors_compressed[i])
                # ベクトルの長さで正規化
                model_norm = np.linalg.norm(model_vectors_compressed[i])
                test_norm = np.linalg.norm(test_vectors_compressed[i])
                if model_norm > 0 and test_norm > 0:
                    dot_products.append(dot_product / (model_norm * test_norm))
            # プロット用に圧縮後のベクトルを代入
            model_vectors = model_vectors_compressed
            test_vectors = test_vectors_compressed

        else:
            # test_vectorsを圧縮
            indices = np.linspace(0, len(test_vectors)-1, len(model_vectors)).astype(int)
            test_vectors_compressed = test_vectors[indices]
            model_vectors_compressed = model_vectors # 圧縮しない方を保持
            min_length = len(model_vectors)
            dot_products = []
            for i in range(min_length):
                # 内積を計算
                dot_product = np.dot(model_vectors_compressed[i], test_vectors_compressed[i])
                # ベクトルの長さで正規化
                model_norm = np.linalg.norm(model_vectors_compressed[i])
                test_norm = np.linalg.norm(test_vectors_compressed[i])
                if model_norm > 0 and test_norm > 0:
                    dot_products.append(dot_product / (model_norm * test_norm))
            # プロット用に圧縮後のベクトルを代入
            model_vectors = model_vectors_compressed
            test_vectors = test_vectors_compressed

        # 同時刻のDTWパスを作成
        path0 = list(range(min_length))
        path1 = list(range(min_length))
        dtw_path = (path0, path1)

    elif mode == 'raw_dot_product_same_time': # 新しいモード
        # 同じ時間フレームで内積を計算 (正規化なし)
        min_length = min(len(model_vectors), len(test_vectors))
        dot_products = []
        for i in range(min_length):
            # 内積をそのまま計算
            dot_product = np.dot(model_vectors[i], test_vectors[i])
            dot_products.append(dot_product)
        # 同時刻のDTWパスを作成
        path0 = list(range(min_length))
        path1 = list(range(min_length))
        dtw_path = (path0, path1)

    else:
        raise ValueError("mode must be one of 'dtw_path', 'dtw_calc', 'same_time', 'relative_time', or 'raw_dot_product_same_time'")

     # プロット (dtw_pathが存在する場合のみ)
    if dtw_path:
        plt.subplot(5, 3, 3 * call - 2)
        plot_alignment(model_vectors[:, 0], test_vectors[:, 0], dtw_path, step = 10)
        plt.subplot(5, 3, 3 * call - 1)
        plot_alignment(model_vectors[:, 1], test_vectors[:, 1], dtw_path, step = 10)
        plt.subplot(5, 3, 3 * call)
        plot_alignment(model_vectors[:, 2], test_vectors[:, 2], dtw_path, step = 10)
    
    # 内積の平均を返す（有効な値のみを使用）
    valid_dot_products = [x for x in dot_products if not np.isnan(x)]
    if len(valid_dot_products) == 0:
        return 0.0
    return np.mean(valid_dot_products)

# CSVファイルを読み込む
df_model = pd.read_csv('Assets/OriginalAssets/File/Exp7_Model/6_pos_linear_rot_slerp.csv')
df_test_file = pd.read_csv('Assets/OriginalAssets/File/Exp7_6/_Te3.csv')
df_t = pd.read_csv('Assets/OriginalAssets/File/Exp7_6/_Te3.csv', dtype=str)

# "Time"列に0.01111111がある行のインデックスを取得
indices = df_t[df_t["time"] == "0.01111111"].index

# インデックスを表示
print(indices.tolist())

# plt.figure(figsize=(12, 15))
# for i in range(0, 1):
#     if i < 4:
#         df_test = df_test_file.loc[indices[i]:indices[i+1]-1]
#     else:
#         df_test = df_test_file.loc[indices[i]:]
#     dtwsw_result = dtw_sw(df_model["PositionX"].to_numpy(), df_model["PositionY"].to_numpy(), df_model["PositionZ"].to_numpy(), df_test["PositionX"].to_numpy(), df_test["PositionY"].to_numpy(), df_test["PositionZ"].to_numpy(), 12, 1.0, factor=180)
#     dtw_score = dtwDistance(df_model["PositionX"].to_numpy(), df_model["PositionY"].to_numpy(), df_model["PositionZ"].to_numpy(), df_test["PositionX"].to_numpy(), df_test["PositionY"].to_numpy(), df_test["PositionZ"].to_numpy(), dtwsw_result[3][0], dtwsw_result[3][1])
#     #サブプロットを作成
#     plt.subplot(5, 3, 3*i + 1)  # 3行1列のi+1番目のサブプロット
#     plot_alignment(df_model["PositionX"].to_numpy(), df_test["PositionX"].to_numpy(), dtwsw_result[3], step = 10)
#     plt.subplot(5, 3, 3*i + 2)
#     plot_alignment(df_model["PositionY"].to_numpy(), df_test["PositionY"].to_numpy(), dtwsw_result[3], step = 10)
#     plt.subplot(5, 3, 3*i + 3)
#     plot_alignment(df_model["PositionZ"].to_numpy(), df_test["PositionZ"].to_numpy(), dtwsw_result[3], step = 10)
#     # plt.subplot(5, 3, 3*i + 4)
#     # plot_costmatrix(dtwsw_result[1], dtwsw_result[3])
#     # plt.subplot(5, 3, 3*i + 5)
#     # plot_costmatrix(dtwsw_result[2], dtwsw_result[3])
    
#     print(dtw_score)
#     euc_score = eucDistance(df_model["PositionX"].to_numpy(), df_model["PositionY"].to_numpy(), df_model["PositionZ"].to_numpy(), df_test["PositionX"].to_numpy(), df_test["PositionY"].to_numpy(), df_test["PositionZ"].to_numpy())
#     euc_rotDis = eucQuaternion(df_model["RotationQX"].to_numpy(), df_model["RotationQY"].to_numpy(), df_model["RotationQZ"].to_numpy(), df_model["RotationQW"].to_numpy(), df_test["RotationQX"].to_numpy(), df_test["RotationQY"].to_numpy(), df_test["RotationQZ"].to_numpy(), df_test["RotationQW"].to_numpy())
#     print(euc_score)
#     print(euc_rotDis)
# plt.tight_layout()  # レイアウトを調整
# plt.show()

plt.figure(figsize=(12, 15))
for i in range(0, 1):
    if i < 0:
        df_test = df_test_file.loc[indices[i]:indices[i+1]-1]
    else:
        df_test = df_test_file.loc[indices[i]:]
    
    #1. 与えられたDTWパスを使用する場合
    # dtw_path = dtwsw_result[3]  # dtw_swの結果からパスを取得
    # dot_product1 = calculate_vector_dot_product(df_model, df_test, mode='dtw_path', dtw_path=dtw_path, call=i+1)

    # #2. ベクトルでDTWを計算する場合
    # dot_product2 = calculate_vector_dot_product(df_model, df_test, mode='dtw_calc', call=i+1)

    #3. 同じ時間フレームで計算する場合 (正規化あり)
    dot_product3 = calculate_vector_dot_product(df_model, df_test, mode='same_time', call=i+1)

    # #4. 長い時系列を圧縮して同じ時間フレームで計算 (正規化あり)
    # dot_product4 = calculate_vector_dot_product(df_model, df_test, mode='relative_time', call=i+1)

    #5. 同じ時間フレームで計算する場合 (正規化なし)
    dot_product5 = calculate_vector_dot_product(df_model, df_test, mode='raw_dot_product_same_time', call=i+1)


    print(dot_product5)

plt.tight_layout()  # レイアウトを調整
plt.show()

