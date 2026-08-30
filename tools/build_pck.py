"""Build OverflowChain.pck - same pipeline as BigHammer (WebP ctex + import)."""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from pck_tool import write_pck  # noqa: E402
from PIL import Image  # noqa: E402

import hashlib  # noqa: E402
import io  # noqa: E402
import struct  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent  # repo root
ASSETS = ROOT / "assets"
OUT = ROOT / "build" / "OverflowChain.pck"

FORMAT_RGBA8 = 5
DATA_FORMAT_WEBP = 2
ICON = "images/relics/overflow_chain.png"


def make_ctex(webp_bytes: bytes, width: int, height: int) -> bytes:
    header = b"GST2"
    header += struct.pack("<I", 1)
    header += struct.pack("<II", width, height)
    header += struct.pack("<I", 0)
    header += struct.pack("<I", 0)
    header += struct.pack("<III", 0, 0, 0)
    header += struct.pack("<I", DATA_FORMAT_WEBP)
    header += struct.pack("<HH", width, height)
    header += struct.pack("<I", 0)
    header += struct.pack("<I", FORMAT_RGBA8)
    header += struct.pack("<I", len(webp_bytes))
    return header + webp_bytes


def imported_name(res_path: str) -> str:
    digest = hashlib.md5(res_path.encode("utf-8")).hexdigest()
    stem = res_path.rsplit("/", 1)[-1]
    return f"{stem}-{digest}.ctex"


def texture_entries(res_path: str, image_path: Path) -> dict:
    im = Image.open(image_path)
    if im.size != (256, 256):
        raise SystemExit(f"{image_path} must be 256x256, got {im.size}")
    if im.mode != "RGBA":
        im = im.convert("RGBA")
    buf = io.BytesIO()
    im.save(buf, format="WEBP", lossless=True)
    ctex_name = imported_name(res_path)
    import_text = f"""[remap]

importer="texture"
type="CompressedTexture2D"
path="res://.godot/imported/{ctex_name}"

[deps]

source_file="res://{res_path}"
dest_files=["res://.godot/imported/{ctex_name}"]

[params]
"""
    return {
        res_path + ".import": import_text.encode("utf-8"),
        ".godot/imported/" + ctex_name: make_ctex(buf.getvalue(), *im.size),
    }


def localization_entries() -> dict:
    entries = {}
    for lang in ("eng", "zhs"):
        for file in sorted((ROOT / "localization" / lang).glob("*.json")):
            entries[f"OverflowChain/localization/{lang}/{file.name}"] = file.read_bytes()
    return entries


def build() -> Path:
    files: dict[str, bytes] = {}
    files.update(texture_entries(ICON, ASSETS / "overflow_chain.png"))
    files.update(localization_entries())
    OUT.parent.mkdir(parents=True, exist_ok=True)
    write_pck(OUT, files, engine=(4, 5, 1))
    print(f"wrote {OUT} ({OUT.stat().st_size} bytes, {len(files)} files)")
    return OUT


if __name__ == "__main__":
    build()
