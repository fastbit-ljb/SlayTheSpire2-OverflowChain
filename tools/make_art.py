"""Generate the Overflow Chain relic icon (256x256) and workshop cover (1024x480).

v3: canonical lightning-bolt silhouette polygon + two interlocked chain rings
at the bottom, soft radial glow behind.
"""
from PIL import Image, ImageDraw, ImageFilter

OUTLINE = (32, 24, 10, 255)
MID = (250, 190, 36, 255)
HI = (255, 250, 214, 255)
METAL = (216, 180, 66, 255)
METAL_HI = (255, 236, 160, 255)

# classic bolt silhouette, normalized points (x right, y down)
BOLT = [(0.55, 0.02), (0.18, 0.50), (0.42, 0.50), (0.30, 0.98),
        (0.78, 0.40), (0.50, 0.40), (0.72, 0.02)]


def scale_pts(pts, cx, cy, sx, sy):
    return [(cx + (x - 0.5) * sx, cy + (y - 0.5) * sy) for x, y in pts]


def draw_link(d, cx, cy, rx, ry, t):
    d.ellipse([cx - rx - t, cy - ry - t, cx + rx + t, cy + ry + t], fill=OUTLINE)
    d.ellipse([cx - rx, cy - ry, cx + rx, cy + ry], fill=METAL)
    d.ellipse([cx - rx + t, cy - ry + t, cx + rx - t, cy + ry - t], fill=(0, 0, 0, 0))
    d.arc([cx - rx + t, cy - ry + t, cx + rx - t, cy + ry - t], 190, 340,
          fill=METAL_HI, width=max(2, int(t * 0.55)))


def make_icon(path):
    s = 256 * 4
    im = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    cx = s / 2
    link_cy = s * 0.76
    link_rx, link_ry, link_t = s * 0.135, s * 0.088, s * 0.040

    # soft glow behind everything
    glow = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    gd.ellipse([s * 0.14, s * 0.10, s * 0.86, s * 0.82], fill=(255, 200, 60, 70))
    glow = glow.filter(ImageFilter.GaussianBlur(s * 0.06))
    im.alpha_composite(glow)

    # interlocked chain rings
    draw_link(d, cx + link_rx * 0.95, link_cy, link_rx, link_ry, link_t)
    draw_link(d, cx - link_rx * 0.95, link_cy, link_rx, link_ry, link_t)
    # overlap wedge so the left ring reads as "in front"
    d.pieslice([cx - link_rx * 1.95 - link_t, link_cy - link_ry - link_t,
                cx - link_rx * 0.05 + link_t, link_cy + link_ry + link_t],
               115, 245, fill=OUTLINE)
    d.pieslice([cx - link_rx * 1.95, link_cy - link_ry,
                cx - link_rx * 0.05, link_cy + link_ry],
               115, 245, fill=METAL)
    d.pieslice([cx - link_rx * 1.95 + link_t, link_cy - link_ry + link_t,
                cx - link_rx * 0.05 - link_t, link_cy + link_ry - link_t],
               115, 245, fill=(0, 0, 0, 0))

    # bolt: outline pass then fill passes
    for scale, col in ((1.0, OUTLINE), (0.86, MID), (0.52, HI)):
        pts = scale_pts(BOLT, cx, s * 0.42, s * 0.40 * scale, s * 0.78 * scale)
        d.polygon(pts, fill=col)

    im = im.resize((256, 256), Image.LANCZOS)
    im.save(path)
    print("icon saved", path)


def make_cover(icon_path, out_path, size=(1024, 480)):
    w, h = size[0] * 2, size[1] * 2
    im = Image.new("RGB", (w, h))
    d = ImageDraw.Draw(im)
    top, mid, bot = (14, 18, 36), (32, 25, 56), (56, 30, 52)
    for y in range(h):
        t = y / h
        if t < 0.6:
            k = t / 0.6
            col = tuple(int(top[i] + (mid[i] - top[i]) * k) for i in range(3))
        else:
            k = (t - 0.6) / 0.4
            col = tuple(int(mid[i] + (bot[i] - mid[i]) * k) for i in range(3))
        d.line([(0, y), (w, y)], fill=col)
    import random
    rng = random.Random(11)
    for _ in range(26):
        x, y = rng.uniform(0, w), rng.uniform(0, h * 0.55)
        r = rng.uniform(60, 190)
        d.ellipse([x - r, y - r * 0.4, x + r, y + r * 0.4], fill=(10, 12, 26))
    im = im.filter(ImageFilter.GaussianBlur(18))
    d = ImageDraw.Draw(im)

    glow = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    main = [(w * 0.34, -20), (w * 0.40, h * 0.22), (w * 0.34, h * 0.26),
            (w * 0.46, h * 0.66), (w * 0.41, h * 0.63), (w * 0.32, h * 1.02)]
    gd.line(main, fill=(120, 100, 255, 60), width=int(w * 0.075), joint="curve")
    glow = glow.filter(ImageFilter.GaussianBlur(30))
    im.paste(Image.new("RGB", (w, h), (90, 80, 200)), (0, 0), glow)

    bolt_layer = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    bd = ImageDraw.Draw(bolt_layer)
    bands = [0.016, 0.013, 0.012, 0.015, 0.013, 0.010]
    left = [(p[0] - w * b, p[1]) for p, b in zip(main, bands)]
    right = [(p[0] + w * b, p[1]) for p, b in zip(main, bands)]
    for scale, col in ((1.0, (28, 22, 40, 255)), (0.72, (255, 200, 60, 255)), (0.34, (255, 250, 220, 255))):
        l = [(w * 0.34 + (x - w * 0.34) * scale, y) for x, y in left]
        r = [(w * 0.34 + (x - w * 0.34) * scale, y) for x, y in right]
        bd.polygon(l + r[::-1], fill=col)
    im.paste(bolt_layer, (0, 0), bolt_layer)

    tipx, tipy = main[-1]
    for _ in range(24):
        x = tipx + rng.uniform(-w * 0.08, w * 0.08)
        y = tipy + rng.uniform(-h * 0.05, h * 0.08)
        r = rng.uniform(1.5, 4.5)
        d.ellipse([x - r, y - r, x + r, y + r], fill=(255, 240, 180))

    icon = Image.open(icon_path).convert("RGBA").resize((h * 5 // 8, h * 5 // 8), Image.LANCZOS)
    halo = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    hd = ImageDraw.Draw(halo)
    hx, hy = int(w * 0.735), int(h * 0.48)
    hd.ellipse([hx - icon.size[0] // 2 - 40, hy - icon.size[1] // 2 - 40,
                hx + icon.size[0] // 2 + 40, hy + icon.size[1] // 2 + 40],
               fill=(120, 96, 40, 110))
    halo = halo.filter(ImageFilter.GaussianBlur(40))
    im.paste(Image.new("RGB", (w, h), (180, 150, 60)), (0, 0), halo)
    im.paste(icon, (hx - icon.size[0] // 2, hy - icon.size[1] // 2), icon)

    from PIL import ImageFont
    zh = ImageFont.truetype(r"C:\Windows\Fonts\msyhbd.ttc", int(h * 0.24))
    en = ImageFont.truetype(r"C:\Windows\Fonts\arialbd.ttf", int(h * 0.085))
    sub = ImageFont.truetype(r"C:\Windows\Fonts\msyh.ttc", int(h * 0.052))

    def text_with_outline(draw, pos, text, font, fill, outline_fill=(20, 16, 8), ow=6):
        x, y = pos
        for dx in range(-ow, ow + 1, 2):
            for dy in range(-ow, ow + 1, 2):
                draw.text((x + dx, y + dy), text, font=font, fill=outline_fill)
        draw.text(pos, text, font=font, fill=fill)

    text_with_outline(d, (int(w * 0.055), int(h * 0.13)), "溢出链", zh, (255, 224, 120))
    d.text((int(w * 0.062), int(h * 0.46)), "OVERFLOW CHAIN", font=en, fill=(240, 238, 255))
    d.text((int(w * 0.062), int(h * 0.60)), "稀有遗物 · 击杀溢出伤害 连锁弹跳", font=sub, fill=(200, 196, 220))
    d.line([(int(w * 0.062), int(h * 0.435)), (int(w * 0.30), int(h * 0.435))], fill=(255, 224, 120), width=3)

    im = im.resize(size, Image.LANCZOS)
    import os
    im.save(out_path, optimize=True)
    sz = os.path.getsize(out_path)
    print("cover saved", out_path, sz, "bytes")
    if sz > 1024 * 1024:
        q = im.quantize(colors=256, method=Image.MEDIANCUT, dither=Image.FLOYDSTEINBERG)
        q.save(out_path, optimize=True)
        print("quantized ->", os.path.getsize(out_path), "bytes")


if __name__ == "__main__":
    make_icon(r"D:\L\Study\mod\OverflowChain\assets\overflow_chain.png")
    make_cover(r"D:\L\Study\mod\OverflowChain\assets\overflow_chain.png",
               r"D:\L\Study\mod\OverflowChain\assets\cover.png")
