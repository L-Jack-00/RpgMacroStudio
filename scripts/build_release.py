import os
import sys
import shutil
import subprocess
import zipfile
import re

if sys.stdout.encoding != 'utf-8':
    try:
        sys.stdout.reconfigure(encoding='utf-8')
    except Exception:
        pass

def get_current_version(root):
    ver_path = os.path.join(root, "src", "RpgMacroStudio.Core", "Data", "VersionInfo.cs")
    if os.path.exists(ver_path):
        with open(ver_path, "r", encoding="utf-8") as f:
            m = re.search(r'CurrentVersion\s*=\s*"(.*?)"', f.read())
            if m:
                return m.group(1)
    return "3.1.3"

def main():
    root = r"C:\Users\SBK\.gemini\antigravity\scratch\RpgMacroStudio"
    parent = os.path.dirname(root)
    release_dir = os.path.join(root, "release")
    dist_dir = os.path.join(parent, "RpgMacroStudio_Dist")
    ver = get_current_version(root)

    if os.path.exists(release_dir):
        shutil.rmtree(release_dir)
    os.makedirs(release_dir, exist_ok=True)

    data_dir = os.path.join(release_dir, "data")
    launcher_temp = os.path.join(release_dir, "_launcher_temp")
    os.makedirs(data_dir, exist_ok=True)
    os.makedirs(launcher_temp, exist_ok=True)

    print(f"=== [1] 본체 앱 dotnet publish 시작 (data/ 서브폴더 격리, v{ver}) ===")
    cmd_publish_app = [
        "dotnet", "publish",
        os.path.join(root, "src", "RpgMacroStudio", "RpgMacroStudio.csproj"),
        "-c", "Release",
        "-r", "win-x64",
        "--self-contained", "false",
        "-o", data_dir
    ]
    res = subprocess.run(cmd_publish_app, cwd=root, capture_output=True, text=True, encoding="utf-8")
    if res.returncode != 0:
        print("Publish failed! STDERR:\n", res.stderr)
        return False
    print("본체 앱 publish 완료 (data/ 격리 성공)!")

    print(f"=== [2] 루트 런처 단일 파일 publish (v{ver}) ===")
    cmd_publish_launcher = [
        "dotnet", "publish",
        os.path.join(root, "src", "RpgMacroStudio.Launcher", "RpgMacroStudio.Launcher.csproj"),
        "-c", "Release",
        "-r", "win-x64",
        "--self-contained", "false",
        "-p:PublishSingleFile=true",
        "-o", launcher_temp
    ]
    res_l = subprocess.run(cmd_publish_launcher, cwd=root, capture_output=True, text=True, encoding="utf-8")
    if res_l.returncode != 0:
        print("Launcher publish failed! STDERR:\n", res_l.stderr)
        return False

    launcher_exe = os.path.join(launcher_temp, "RpgMacroStudio.exe")
    root_launcher_exe = os.path.join(release_dir, "RpgMacroStudio.exe")
    shutil.copy2(launcher_exe, root_launcher_exe)
    shutil.rmtree(launcher_temp)
    print("루트 런처 배치 완료 (RpgMacroStudio.exe)!")

    print(f"=== [3] 릴리즈 바이너리 자체 테스트 (--test) 검증 ===")
    res_test = subprocess.run([root_launcher_exe, "--test"], capture_output=True)
    out_str = res_test.stdout.decode("utf-8", errors="replace")
    print("Test Return code:", res_test.returncode)
    if res_test.returncode != 0:
        print("Self-test failed on release binary!")
        print("STDOUT:\n", out_str)
        print("STDERR:\n", res_test.stderr.decode("utf-8", errors="replace"))
        return False
    print("Self-test passed (Return code: 0)!")

    if os.path.exists(dist_dir):
        try:
            shutil.rmtree(dist_dir)
        except Exception:
            subprocess.run(["taskkill", "/F", "/IM", "RpgMacroStudio.exe"], capture_output=True)
            time.sleep(0.5)
            shutil.rmtree(dist_dir, ignore_errors=True)
    os.makedirs(dist_dir, exist_ok=True)
    for item in os.listdir(release_dir):
        s = os.path.join(release_dir, item)
        d = os.path.join(dist_dir, item)
        if not item.endswith(".zip"):
            if os.path.isdir(s):
                shutil.copytree(s, d)
            else:
                shutil.copy2(s, d)
    print("RpgMacroStudio_Dist synchronized!")

    skip_zip = "--no-zip" in sys.argv or "--quick" in sys.argv
    if skip_zip:
        print("=== [5] ZIP 패키징 건너뜀 (--quick / --no-zip 모드) ===")
    else:
        print(f"=== [5] ZIP 패키징 (v{ver}) ===")
        zip_name = f"RpgMacroStudio_v{ver}_win-x64.zip"
        release_zip = os.path.join(release_dir, zip_name)
        parent_zip = os.path.join(parent, zip_name)

        # 기존 오래된 zip 파일 삭제
        for p in [release_zip, parent_zip]:
            if os.path.exists(p):
                os.remove(p)

        with zipfile.ZipFile(release_zip, "w", zipfile.ZIP_DEFLATED) as zf:
            for root_dir, dirs, files in os.walk(release_dir):
                for file in files:
                    if file.endswith(".zip"):
                        continue
                    full_path = os.path.join(root_dir, file)
                    rel_path = os.path.relpath(full_path, release_dir)
                    zf.write(full_path, arcname=rel_path)

        shutil.copy2(release_zip, parent_zip)
        print(f"ZIP created: {release_zip} ({round(os.path.getsize(release_zip)/(1024*1024), 2)} MB)")
        print(f"ZIP copied: {parent_zip}")

    print("=== [6] 배포 프로세스 완료 ===")
    return True

if __name__ == "__main__":
    assert main()
