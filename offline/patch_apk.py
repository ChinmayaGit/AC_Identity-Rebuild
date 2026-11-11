#!/usr/bin/env python3
"""Patch AC Identity so discovery, response signatures, and Play licensing stay on this machine."""

import subprocess
import sys
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
APK = ROOT / "Resorces" / "ACIdentity.apk"
OUT_DIR = ROOT / "offline"
KEYSTORE = OUT_DIR / "debug.keystore"

is_localhost = "--localhost" in sys.argv
if is_localhost:
    NEW_URL = b"http://127.0.0.1:8080/discovery/acier/v1/api"
    UNSIGNED = OUT_DIR / "ACIdentity-offline-localhost-unsigned.apk"
    SIGNED = OUT_DIR / "ACIdentity-offline-localhost.apk"
else:
    NEW_URL = b"http://192.168.1.2:8080/discovery/acier/v1/x"
    UNSIGNED = OUT_DIR / "ACIdentity-offline-unsigned.apk"
    SIGNED = OUT_DIR / "ACIdentity-offline.apk"

OLD_URL = b"https://ac-id.ubi.com/discovery/acier/v1/api"
ASSET = "assets/bin/Data/400fee16fca9d6044aaacba59277df41"
DLL = "assets/bin/Data/Managed/Assembly-CSharp.dll"
CACHE = "assets/bin/Data/Managed/SharedBaseLib.dll"

# NetworkDataStore..ctor passes true into SharedNetworkDataStore's signature flag.
SIGNATURE_OLD = bytes.fromhex("02031728ee4b0006")
SIGNATURE_NEW = bytes.fromhex("02031628ee4b0006")
# LicenseCheckAndroid.StartLicenseCheck calls the base (which already succeeds) and then Play.
# Return immediately after that base call.
LICENSE_OLD = bytes.fromhex("020328761d000628")
LICENSE_NEW = bytes.fromhex("020328761d00062a")
# Skip the compressed-file CRC check. Recompression cannot reproduce Ubisoft's bytes,
# and a mismatch is reported as startup error 6007.
CRC_OLD = bytes.fromhex("027b2a6800046fb425000a027b296800043b3d000000")
CRC_NEW = bytes.fromhex("0000000000000000000000000000000000383d000000")
# PrepareFile takes a ranged GET sized to the original CDN file. Our recompressed
# zlib is a different length, and that mismatch is reported as "no internet".
# nop out the size load, then branch unconditionally. A bare br would leave the
# size on the stack and the runtime rejects the method.
RANGE_OLD = bytes.fromhex("027b4904000439ff000000")
RANGE_NEW = bytes.fromhex("00000000000038ff000000")
# The low-quality camera is forced onto the vertex-lit path. The city and the
# assassin shaders only have a forward pass, so that path draws neither of them.
# ldc.i4.0 (VertexLit) becomes ldc.i4.1 (Forward) at the one set_renderingPath call.
RENDER_OLD = bytes.fromhex("166FE610000A")
RENDER_NEW = bytes.fromhex("176FE610000A")
# Modern Snapdragon phones report DXT1 support, causing the game to request DXT bundles
# which are not in the cache. Force DeviceManager.GetMostOptimalTextureType to return ETC2 (2).
TEXTURE_OLD = bytes.fromhex("1f0a28b82a000a3902000000172a")
TEXTURE_NEW = bytes.fromhex("182a000000000000000000000000")
# UIDownloadScreen.GetSceneBundles looks up scene bundles to download.
# In offline mode, all bundles are local. Returning null immediately bypasses
# the null reference in EarlyAccessHelpers.GetMapping and skips the download dialog.
DOWNLOAD_OLD = bytes.fromhex("036f9c25000a036f9d25000a036f9e25000a289b730006")
DOWNLOAD_NEW = bytes.fromhex("142a000000000000000000000000000000000000000000")


def replace_unique(data: bytearray, old: bytes, new: bytes, label: str) -> None:
    at = data.find(old)
    if at < 0 or data.find(old, at + 1) >= 0:
        raise SystemExit(label + " pattern was not unique")
    data[at:at + len(old)] = new


def patch_dll(data: bytes) -> bytes:
    data = bytearray(data)
    replace_unique(data, SIGNATURE_OLD, SIGNATURE_NEW, "signature call site")
    replace_unique(data, LICENSE_OLD, LICENSE_NEW, "license call site")
    replace_unique(data, CRC_OLD, CRC_NEW, "bundle crc check")
    replace_unique(data, RENDER_OLD, RENDER_NEW, "camera rendering path")
    replace_unique(data, TEXTURE_OLD, TEXTURE_NEW, "texture format override")
    replace_unique(data, DOWNLOAD_OLD, DOWNLOAD_NEW, "scene bundles download bypass")
    return bytes(data)


def patch_cache(data: bytes) -> bytes:
    data = bytearray(data)
    replace_unique(data, RANGE_OLD, RANGE_NEW, "download range branch")
    return bytes(data)


def patch_dex(data: bytes) -> bytes:
    import struct
    import hashlib
    import zlib
    dex = bytearray(data)
    code_off = 0x476044
    target = bytes.fromhex("1200390903000e00")
    if dex[code_off + 16:code_off + 16 + len(target)] != target:
        raise SystemExit("classes.dex permission check site not found")
    # Bypass Unity's deprecated android.app.Fragment permission request which crashes on Android 14+
    # (Fragment null must be a public static class to be properly recreated from instance state).
    # v10 is the Runnable argument (Unity startup callback).
    # Replace the initial 4 code units (8 bytes: const/4 v0, #0; if-nez v9, +3; return-void)
    # with:
    # 72 10 d5 d4 0a 00 (invoke-interface/range {v10}, Ljava/lang/Runnable;->run:()V) [3 code units]
    # 0e 00 (return-void) [1 code unit]
    # Exactly 4 code units (8 bytes) perfectly aligns with Try 0 at pc 4.
    patch = bytes.fromhex("7210d5d40a000e00")
    dex[code_off + 16:code_off + 16 + len(patch)] = patch

    # Recompute SHA1 checksum
    sha1 = hashlib.sha1(dex[32:]).digest()
    dex[12:32] = sha1

    # Recompute Adler32 checksum
    adler = zlib.adler32(dex[12:]) & 0xffffffff
    dex[8:12] = struct.pack("<I", adler)
    return bytes(dex)


def main() -> None:
    if len(OLD_URL) != len(NEW_URL):
        raise SystemExit(f"url length changed {len(OLD_URL)} -> {len(NEW_URL)}")
    blob = zipfile.ZipFile(APK).read(ASSET)
    if blob.count(OLD_URL) != 1:
        raise SystemExit("discovery url was not found once in the build definition")
    blob = blob.replace(OLD_URL, NEW_URL)
    dll = patch_dll(zipfile.ZipFile(APK).read(DLL))
    cache = patch_cache(zipfile.ZipFile(APK).read(CACHE))

    if UNSIGNED.exists():
        UNSIGNED.unlink()
    with zipfile.ZipFile(APK, "r") as source, zipfile.ZipFile(UNSIGNED, "w") as dest:
        for info in source.infolist():
            if info.filename.startswith("META-INF/"):
                continue
            payload = source.read(info.filename)
            if info.filename == "AndroidManifest.xml":
                manifest_bytes = bytearray(payload)
                # Patch targetSdkVersion from 23 to 26 (Android 8.0 Oreo):
                # 1. Bypasses the minimum SDK install block on Android 14/15/16 (requires >= 24)
                # 2. Fully allows cleartext HTTP (http://127.0.0.1:8080) by default (Android only blocked it in API 28+)
                target_offset = manifest_bytes.find(b"\x08\x00\x00\x10\x17\x00\x00\x00")
                if target_offset != -1:
                    manifest_bytes[target_offset + 4] = 26
                payload = bytes(manifest_bytes)
            elif info.filename == ASSET:
                payload = blob
            elif info.filename == DLL:
                payload = dll
            elif info.filename == CACHE:
                payload = cache
            elif info.filename == "classes.dex":
                payload = patch_dex(payload)
            info.flag_bits &= ~0x8
            dest.writestr(info, payload)

    if not KEYSTORE.exists():
        subprocess.check_call([
            "keytool", "-genkeypair", "-keystore", str(KEYSTORE),
            "-storepass", "offline", "-keypass", "offline", "-alias", "acid",
            "-keyalg", "RSA", "-keysize", "2048", "-validity", "10000",
            "-dname", "CN=AC Identity Offline",
        ])
    if SIGNED.exists():
        SIGNED.unlink()
    build_tools = Path("/opt/homebrew/share/android-commandlinetools/build-tools/36.1.0")
    aligned = OUT_DIR / "ACIdentity-offline-aligned.apk"
    if aligned.exists():
        aligned.unlink()
    subprocess.check_call([str(build_tools / "zipalign"), "-p", "-f", "4", str(UNSIGNED), str(aligned)])
    subprocess.check_call([
        str(build_tools / "apksigner"), "sign",
        "--ks", str(KEYSTORE), "--ks-key-alias", "acid",
        "--ks-pass", "pass:offline", "--key-pass", "pass:offline",
        "--v1-signing-enabled", "true", "--v2-signing-enabled", "true", "--v3-signing-enabled", "true",
        "--out", str(SIGNED), str(aligned),
    ])
    aligned.unlink()
    subprocess.check_call([str(build_tools / "apksigner"), "verify", "-v", str(SIGNED)])
    print(SIGNED)


if __name__ == "__main__":
    main()
