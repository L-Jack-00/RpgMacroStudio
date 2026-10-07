import subprocess
import sys
import os
import time

if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8')

def run():
    start = time.time()
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    print("⚡ [고속 검증 루프 시작 (Hot-Loop)]")

    # 1. dotnet build
    print("  [1/3] dotnet build 검사 중...")
    b_res = subprocess.run(["dotnet", "build", "--nologo", "-v", "q"], cwd=root, capture_output=True, text=True, encoding="utf-8")
    if b_res.returncode != 0:
        print("  ❌ 빌드 실패!")
        print(b_res.stderr or b_res.stdout)
        return False

    # 2. --test 실행
    print("  [2/3] 코어 자체 테스트 (--test) 검증 중...")
    t_res = subprocess.run(["dotnet", "run", "--project", "src/RpgMacroStudio/RpgMacroStudio.csproj", "--no-build", "--", "--test"], cwd=root, capture_output=True)
    if t_res.returncode != 0:
        print("  ❌ 자체 테스트 실패!")
        return False

    # 3. inspect_agents.py --compact
    print("  [3/3] 12대 에이전트 종합 감사 검증 중...")
    i_res = subprocess.run([sys.executable, os.path.join(root, "scripts", "inspect_agents.py"), "--compact"], cwd=root, capture_output=True, text=True, encoding="utf-8")
    if i_res.returncode != 0:
        print("  ❌ 12대 감사 실패:\n", i_res.stdout)
        return False
    print(" ", i_res.stdout.strip())

    elapsed = round(time.time() - start, 2)
    print(f"🎉 ⚡ [초고속 검증 통과] 빌드 + 자체 테스트 + 12대 감사 완료 ({elapsed}초 소요)")
    return True

if __name__ == "__main__":
    success = run()
    sys.exit(0 if success else 1)
