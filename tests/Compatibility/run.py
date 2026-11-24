"""Run the optional compatibility fixture under the installed game's Mono (Windows)."""
import argparse
import ctypes as c
import os
from pathlib import Path
import subprocess
import sys


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("more_relics", nargs="?", type=Path)
    parser.add_argument("bakery_uses_wheat", nargs="?", type=Path)
    parser.add_argument("--game", type=Path, default=Path(r"D:\SteamLibrary\steamapps\common\Super Fantasy Kingdom"))
    default_profile = Path(os.environ["APPDATA"]) / "Thunderstore Mod Manager/DataFolder/SuperFantasyKingdom/profiles/Daybook Dev"
    parser.add_argument("--profile", type=Path, default=default_profile)
    options = parser.parse_args()
    packs = [p.resolve(strict=True) for p in [options.more_relics, options.bakery_uses_wheat] if p]
    root = Path(__file__).resolve().parents[2]
    work = root / ".work/compatibility"
    work.mkdir(exist_ok=True, parents=True)
    managed = options.game.resolve() / "Super Fantasy Kingdom_Data/Managed"
    runtime = options.game.resolve() / "MonoBleedingEdge"
    core = options.profile.resolve() / "BepInEx/core"
    plugin = (root / "bin/Release/netstandard2.1/AmayaWithoutCooldown.dll").resolve(strict=True)
    sdk_line = subprocess.check_output(["dotnet", "--list-sdks"], text=True).splitlines()[-1]
    version, base = sdk_line.split(" [", 1)
    compiler = Path(base.rstrip("]")) / version / "Roslyn/bincore/csc.dll"
    exe = work / ("ShopFixture-" + str(len(packs)) + ".exe")
    assemblies = ["mscorlib", "System", "System.Core", "netstandard", "Assembly-CSharp",
                  "UnityEngine.CoreModule", "UnityEngine", "UnityEngine.UI"]
    refs = [f"/reference:{managed / (name + '.dll')}" for name in assemblies]
    refs.extend(f"/reference:{core / name}" for name in ["0Harmony.dll", "BepInEx.dll"])
    subprocess.run(["dotnet", str(compiler), "/nologo", "/noconfig", "/nostdlib", "/target:exe",
                    f"/out:{exe}", *refs, str(Path(__file__).with_name("ShopFixture.cs").resolve())], check=True)

    os.chdir(work)
    dll_directory = os.add_dll_directory(str(runtime / "EmbedRuntime"))
    mono = c.CDLL(str(runtime / "EmbedRuntime/mono-2.0-bdwgc.dll"))

    def api(name, result, *signature):
        f = getattr(mono, name)
        f.restype = result
        f.argtypes = signature
        return f

    string, pointer, integer = c.c_char_p, c.c_void_p, c.c_int
    api("mono_set_dirs", None, string, string)(str(managed).encode(), str(runtime / "etc").encode())
    api("mono_set_assemblies_path", None, string)(f"{managed};{core}".encode())
    domain = api("mono_jit_init_version", pointer, string, string)(b"AmayaFixture", b"v4.0.30319")
    # Supply the handful of Unity engine calls that the managed test actually executes.
    # Store callbacks until Mono exits. No player preferences or scene are opened.
    prefs = {}
    chars = api("mono_string_chars", pointer, pointer)
    length = api("mono_string_length", integer, pointer)

    def key(value):
        return c.string_at(chars(value), 2 * length(value)).decode("utf-16-le")

    def write_pref(name, value):
        prefs[key(name)] = value
        return 1

    stubs = {
        "UnityEngine.Shader::PropertyToID": c.CFUNCTYPE(integer, pointer)(lambda _: 1),
        "UnityEngine.Time::get_timeScale": c.CFUNCTYPE(c.c_float)(lambda: 1),
        "UnityEngine.PlayerPrefs::GetInt": c.CFUNCTYPE(integer, pointer, integer)(lambda name, fallback: prefs.get(key(name), fallback)),
        "UnityEngine.PlayerPrefs::TrySetInt": c.CFUNCTYPE(integer, pointer, integer)(write_pref),
        "UnityEngine.Behaviour::get_isActiveAndEnabled": c.CFUNCTYPE(integer, pointer)(lambda _: 1),
    }
    for method, callback in stubs.items():
        api("mono_add_internal_call", None, string, pointer)(method.encode(), c.cast(callback, pointer))
    assembly = api("mono_domain_assembly_open", pointer, pointer, string)(domain, str(exe).encode())
    if not assembly:
        raise RuntimeError(f"Failed to load {exe}")
    argv = [str(p).encode() for p in [exe, plugin, *packs]]
    arguments = (string * len(argv))(*argv)
    code = api("mono_jit_exec", integer, pointer, pointer, integer, c.POINTER(string))(
        domain, assembly, len(argv), arguments)
    dll_directory.close()
    return code


if __name__ == "__main__":
    sys.exit(main())
