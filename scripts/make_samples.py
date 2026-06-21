"""
Генерация тестовых изображений, на которых текущий алгоритм (рельеф = яркость/цвет,
сглаживание) выглядит хорошо: плавные низкочастотные формы, мягкие цветовые палитры,
хороший контраст. Кладёт PNG в wwwroot/uploads (чтобы отдавались сервером).
"""
import os
import numpy as np
from PIL import Image, ImageFilter

# Корень проекта = родитель папки со скриптом (scripts/), чтобы работало из любого cwd
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "wwwroot", "uploads")
os.makedirs(OUT, exist_ok=True)
SIZE = 480


def smooth_noise(size, base_res, octaves=3, seed=0):
    rng = np.random.default_rng(seed)
    acc = np.zeros((size, size), np.float32)
    amp, total, res = 1.0, 0.0, base_res
    for _ in range(octaves):
        small = (rng.random((res, res)) * 255).astype(np.uint8)
        up = Image.fromarray(small).resize((size, size), Image.BICUBIC)
        acc += np.asarray(up, np.float32) / 255.0 * amp
        total += amp
        amp *= 0.5
        res *= 2
    acc /= total
    acc = (acc - acc.min()) / (acc.max() - acc.min() + 1e-6)
    return acc


def colormap(hmap, stops):
    pos = np.array([s[0] for s in stops], np.float32)
    out = np.zeros((*hmap.shape, 3), np.float32)
    for ch in range(3):
        vals = np.array([s[1][ch] for s in stops], np.float32)
        out[..., ch] = np.interp(hmap, pos, vals)
    return out.astype(np.uint8)


def save(arr, name):
    img = Image.fromarray(arr).filter(ImageFilter.GaussianBlur(2.0))
    img.save(os.path.join(OUT, name))
    print("saved", name)


# A. Зелёные холмы — плавный низкочастотный шум, зелёная палитра (стабильно высокий, мягкий рельеф)
hills = smooth_noise(SIZE, 4, 3, seed=7)
save(colormap(hills, [
    (0.0, (28, 72, 34)), (0.45, (92, 150, 58)), (0.75, (158, 188, 96)), (1.0, (212, 214, 140)),
]), "sample_hills.png")

# B. Песчаные дюны — синусоидальные гряды + лёгкий шум, тёплая палитра
yy, xx = np.mgrid[0:SIZE, 0:SIZE].astype(np.float32) / SIZE
dunes = 0.5 + 0.5 * np.sin(xx * np.pi * 4 + np.sin(yy * np.pi * 3) * 1.2)
dunes = 0.7 * dunes + 0.3 * smooth_noise(SIZE, 5, 2, seed=3)
dunes = (dunes - dunes.min()) / (dunes.max() - dunes.min())
save(colormap(dunes, [
    (0.0, (150, 108, 64)), (0.5, (206, 168, 108)), (1.0, (240, 214, 158)),
]), "sample_dunes.png")

# C. Остров — радиальный плавный подъём, вода → пляж → зелень → скала
cx = (xx - 0.5) * 2
cy = (yy - 0.5) * 2
dist = np.sqrt(cx * cx + cy * cy)
island = np.clip(1.0 - dist, 0, 1) ** 1.3
island = 0.75 * island + 0.25 * smooth_noise(SIZE, 5, 3, seed=11)
island = (island - island.min()) / (island.max() - island.min())
save(colormap(island, [
    (0.0, (40, 96, 150)), (0.30, (74, 134, 168)), (0.40, (214, 200, 150)),
    (0.55, (96, 156, 64)), (0.80, (120, 120, 92)), (1.0, (150, 146, 120)),
]), "sample_island.png")

print("done")
