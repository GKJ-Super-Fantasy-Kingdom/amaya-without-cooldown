"""Assemble and verify a local Thunderstore import from the Release build."""
import json
from io import BytesIO
from pathlib import Path
from tempfile import TemporaryDirectory
from xml.etree import ElementTree
from zipfile import ZIP_DEFLATED, ZipFile

from PIL import Image

root = Path(__file__).resolve().parent
listing = root / "thunderstore"
manifest = json.loads((listing / "manifest.json").read_text(encoding="utf-8"))
name = manifest["name"]
version = manifest["version_number"]
project = ElementTree.parse(root / f"{name}.csproj")
if version != project.findtext(".//Version"):
    raise SystemExit("The project and manifest versions differ.")
framework = project.findtext(".//TargetFramework")
dll = f"plugins/{name}.dll"
files = {
    "LICENSE": root / "LICENSE",
    "manifest.json": listing / "manifest.json",
    "icon.png": listing / "icon.png",
    "README.md": listing / "README.md",
    "CHANGELOG.md": root / "CHANGELOG.md",
    dll: root / "bin" / "Release" / framework / f"{name}.dll",
}

out = root / "dist" / f"{name}-{version}.zip"
out.parent.mkdir(exist_ok=True)
with TemporaryDirectory(dir=out.parent) as scratch:
    archive = Path(scratch) / out.name
    with ZipFile(archive, "w", ZIP_DEFLATED) as z:
        for member, source in files.items():
            z.write(source, member)
    with ZipFile(archive) as z:
        if z.testzip() or sorted(z.namelist()) != sorted(files):
            raise SystemExit("Invalid archive members or CRC.")
        for member, source in files.items():
            if z.read(member) != source.read_bytes():
                raise SystemExit(f"Content mismatch: {member}")
        for member in ("README.md", "CHANGELOG.md"):
            z.read(member).decode("utf-8")
        with Image.open(BytesIO(z.read("icon.png"))) as icon:
            if icon.format != "PNG" or icon.size != (256, 256):
                raise SystemExit("Expected a 256 by 256 PNG icon.")
            icon.verify()
    archive.replace(out)
print(out)
