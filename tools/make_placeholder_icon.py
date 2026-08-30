"""TEMPORARY placeholder icon until real art arrives - a simple yellow bolt."""
import math

from PIL import Image, ImageDraw

im = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
d = ImageDraw.Draw(im)

bolt = [(140, 18), (70, 140), (118, 140), (88, 240), (190, 108), (138, 108), (176, 18)]
d.polygon(bolt, fill=(35, 30, 20, 255))
inner = [(x + (128 - x) * 0.18, y + (128 - y) * 0.18) for x, y in bolt]
d.polygon(inner, fill=(255, 224, 64, 255))
core = [(x + (128 - x) * 0.45, y + (128 - y) * 0.45) for x, y in bolt]
d.polygon(core, fill=(255, 250, 210, 255))

im.save(r"D:\L\Study\mod\OverflowChain\assets\overflow_chain.png")
print("placeholder bolt written", im.size)
