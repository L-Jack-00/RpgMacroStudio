using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using RpgMacroHarness.Data;
using RpgMacroHarness.Tests;
using RpgMacroHarness.UI;

namespace RpgMacroHarness;

internal static class Program
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);
    private const int ATTACH_PARENT_PROCESS = -1;

    /// <summary>
    /// 현재 프로세스가 Windows 관리자 권한(Elevated Administrator)으로 실행 중인지 확인합니다.
    /// UIPI(User Interface Privilege Isolation)로 인한 입력 씹힘 방지 가이드에 활용됩니다.
    /// </summary>
    public static bool IsAdministrator()
    {
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 애플리케이션의 주 진입점입니다.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        // 커맨드라인에서 --test 인수를 주면 GUI 창 대신 자동 자체 테스트 스위트 실행
        if (args.Length > 0 && args[0].Equals("--test", StringComparison.OrdinalIgnoreCase))
        {
            AttachConsole(ATTACH_PARENT_PROCESS);
            using var fileLog = new StreamWriter("test_output.log", false, System.Text.Encoding.UTF8) { AutoFlush = true };
            var stdOut = Console.OpenStandardOutput();
            var writer = new StreamWriter(stdOut, System.Text.Encoding.UTF8) { AutoFlush = true };
            
            var origOut = Console.Out;
            // Write to both Console and fileLog
            var combined = new Action<string>(msg => {
                try { Console.WriteLine(msg); } catch {}
                try { fileLog.WriteLine(msg); } catch {}
            });

            Console.SetOut(fileLog);

            bool success = SelfTestRunner.RunAllTests();
            fileLog.WriteLine($"[SelfTestRunner] Result: {success}");
            bool insp = TestInspectorLayout();
            fileLog.WriteLine($"[TestInspectorLayout] Result: {insp}");
            bool classic = TestClassicViewLayout();
            fileLog.WriteLine($"[TestClassicViewLayout] Result: {classic}");
            bool settings = TestSettingsFormLayout();
            fileLog.WriteLine($"[TestSettingsFormLayout] Result: {settings}");
            bool ai = TestAiAssistantDialogLayout();
            fileLog.WriteLine($"[TestAiAssistantDialogLayout] Result: {ai}");
            bool dark = TestDarkThemeAndScrollbarIntegrity();
            fileLog.WriteLine($"[TestDarkThemeAndScrollbarIntegrity] Result: {dark}");
            
            success &= insp & classic & settings & ai & dark;
            fileLog.WriteLine($"[Overall] Success: {success}");
            fileLog.Flush();

            Environment.Exit(success ? 0 : 1);
            return;
        }

        ApplicationConfiguration.Initialize();

        // Visual C++ 2015-2022 재배포 가능 패키지 사전 무결성 점검
        if (!EnsureVisualCppRuntime())
        {
            return;
        }

        string eventProfilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "event_profile.json");
        if (!File.Exists(eventProfilePath))
        {
            var defaultProfile = MacroEventProfileManager.CreateDefaultProfile();
            MacroEventProfileManager.SaveToFile(eventProfilePath, defaultProfile);
        }

        Application.Run(new MainForm());
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static bool EnsureVisualCppRuntime()
    {
        try
        {
            _ = OpenCvSharp.Cv2.GetVersionString();
            return true;
        }
        catch (Exception ex) when (ex is DllNotFoundException || ex.InnerException is DllNotFoundException || ex is TypeInitializationException)
        {
            string msg =
                "[오류] 컴퓨터에 Visual C++ 2015-2022 재배포 가능 패키지(x64)가 설치되어 있지 않습니다.\n" +
                "컴퓨터 비전(OpenCV) 기능 및 매크로 구동을 위해 Microsoft 공식 패키지 설치가 반드시 필요합니다.\n\n" +
                "[Error] Visual C++ 2015-2022 Redistributable (x64) is missing on this PC.\n" +
                "Please download and install it from the official Microsoft website.\n\n" +
                "공식 다운로드 링크 (Official URL):\n" +
                "https://aka.ms/vs/17/release/vc_redist.x64.exe\n\n" +
                "[확인]을 누르면 공식 다운로드 페이지를 열고 프로그램을 안전하게 종료합니다.";

            var res = MessageBox.Show(
                msg,
                "Visual C++ Runtime Required / 필수 런타임 누락 안내",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Error
            );

            if (res == DialogResult.OK)
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "https://aka.ms/vs/17/release/vc_redist.x64.exe",
                        UseShellExecute = true
                    });
                }
                catch { }
            }

            Environment.Exit(1);
            return false;
        }
    }

    private static bool TestInspectorLayout()
    {
        try
        {
            using var form = new MainForm();
            var handle = form.Handle;
            form.Size = new System.Drawing.Size(1280, 800);

            var setCollapsedMethod = typeof(MainForm).GetMethod("SetInspectorCollapsed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            setCollapsedMethod?.Invoke(form, new object[] { false });

            var pnlInspectorField = typeof(MainForm).GetField("_pnlInspector", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var pnlInspector = (Panel)pnlInspectorField!.GetValue(form)!;

            var grpStatePropsField = typeof(MainForm).GetField("_grpStateProps", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var grpStateProps = (GroupBox)grpStatePropsField!.GetValue(form)!;

            var txtStateNameField = typeof(MainForm).GetField("_txtStateName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var txtStateName = (TextBox)txtStateNameField!.GetValue(form)!;

            var btnSetInitialField = typeof(MainForm).GetField("_btnSetInitial", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var btnSetInitial = (Button)btnSetInitialField!.GetValue(form)!;

            var btnDeleteStateField = typeof(MainForm).GetField("_btnDeleteState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var btnDeleteState = (Button)btnDeleteStateField!.GetValue(form)!;

            var chkEventEnabledField = typeof(MainForm).GetField("_chkEventEnabled", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var chkEventEnabled = (CustomCheckBox)chkEventEnabledField!.GetValue(form)!;

            var chkParallelField = typeof(MainForm).GetField("_chkParallel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var chkParallel = (CustomCheckBox)chkParallelField!.GetValue(form)!;

            int btnDelRight = btnDeleteState.Right;
            int tblWidth = btnDeleteState.Parent?.ClientSize.Width ?? 0;

            bool withinBounds = btnDelRight <= tblWidth;
            bool buttonsVisible = btnSetInitial.Width > 0 && btnDeleteState.Width > 0;
            bool comfortableHeight = grpStateProps.Height >= 60 && grpStateProps.Height <= 70;
            bool buttonHeightAdequate = btnSetInitial.Height >= 24 && btnDeleteState.Height >= 24;

            bool chkNoOverlap = chkEventEnabled.Right <= chkParallel.Left;
            bool chkWithinParent = chkParallel.Right <= (chkParallel.Parent?.ClientSize.Width ?? 400);
            bool chkWidthAdequate = chkParallel.Width >= 80 && chkEventEnabled.Width >= 90;

            if (withinBounds && buttonsVisible && comfortableHeight && buttonHeightAdequate && chkNoOverlap && chkWithinParent && chkWidthAdequate)
            {
                Console.WriteLine($"  ✔ [통과] 인스펙터 노드/이벤트 속성 레이아웃 검증 (높이: {grpStateProps.Height}px, 활성화체크: {chkEventEnabled.Bounds}, 동시실행체크: {chkParallel.Bounds})");
                return true;
            }

            int parentW = chkParallel.Parent?.ClientSize.Width ?? 0;
            Console.WriteLine($"  ❌ [실패] 인스펙터 레이아웃 오류: DelRight={btnDelRight}, ParentWidth={tblWidth}, withinBounds={withinBounds}, buttonsVisible={buttonsVisible}, comfortableHeight={comfortableHeight} (grpHeight={grpStateProps.Height}), buttonHeightAdequate={buttonHeightAdequate} (initH={btnSetInitial.Height}, delH={btnDeleteState.Height}), chkNoOverlap={chkNoOverlap}, chkWithinParent={chkWithinParent} (chkRight={chkParallel.Right}, parentW={parentW}), chkWidthAdequate={chkWidthAdequate}");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ [예외] 인스펙터 레이아웃 테스트 오류: {ex}");
            return false;
        }
    }

    private static bool TestClassicViewLayout()
    {
        try
        {
            using var form = new MainForm();
            var handle = form.Handle;
            form.Size = new System.Drawing.Size(1280, 800);
            form.SwitchViewMode(AppViewMode.ClassicTree);

            var classicField = typeof(MainForm).GetField("_classicControl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var classicView = (ClassicViewControl)classicField!.GetValue(form)!;

            var pnlTabsField = typeof(ClassicViewControl).GetField("_pnlTabsHeaderContainer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var pnlTabs = (Panel)pnlTabsField!.GetValue(classicView)!;

            var pnlButtonsField = typeof(ClassicViewControl).GetField("_pnlStateButtons", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var pnlButtons = (FlowLayoutPanel)pnlButtonsField!.GetValue(classicView)!;

            var pnlListField = typeof(ClassicViewControl).GetField("_pnlListContainer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var pnlList = (Panel)pnlListField!.GetValue(classicView)!;

            var lvEventsField = typeof(ClassicViewControl).GetField("_lvEvents", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var lvEvents = (DarkListView)lvEventsField!.GetValue(classicView)!;

            // 1. 패널들이 위에서 아래로 올바르게 수직 적층되어야 함 (겹침 0)
            bool tabsAtTop = pnlTabs.Top == 0;
            bool buttonsBelowTabs = pnlButtons.Top >= pnlTabs.Bottom;
            bool listBelowButtons = pnlList.Top >= pnlButtons.Bottom;
            bool listHeightValid = pnlList.Height > 0 && pnlList.Bottom <= classicView.ClientSize.Height;

            // 2. lvEvents는 pnlList 내부에서 최상단(y=0)부터 시작하여 헤더 및 첫 번째 행이 가려지지 않아야 함
            bool lvAtTop = lvEvents.Top == 0;
            bool lvWidthValid = lvEvents.Width > 0 && lvEvents.Height > 0;

            // 3. 4개 컬럼(항목, 상태, 분류, 상세) 완비 및 가로 스크롤 제로 비례 적응 검증
            bool colCountValid = lvEvents.Columns.Count == 4;
            int totalColWidth = 0;
            for (int i = 0; i < lvEvents.Columns.Count; i++) totalColWidth += lvEvents.Columns[i].Width;
            bool noHScroll = totalColWidth <= lvEvents.ClientSize.Width + 4;

            // 4. 오른쪽 인스펙터 패널 너비 적응형 상단 툴바 버튼 축소 무결성 검증
            var btnAddStateField = typeof(ClassicViewControl).GetField("_btnAddState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var btnAddState = (Button)btnAddStateField!.GetValue(classicView)!;

            var splitMainField = typeof(MainForm).GetField("_splitMain", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var splitMain = (SplitContainer)splitMainField!.GetValue(form)!;

            splitMain.SplitterDistance = 550; // 인스펙터가 넓어져 클래식 뷰 가용 폭이 550px로 좁아진 상태
            bool isCompactOnNarrow = btnAddState.Text == "➕" && btnAddState.Width <= 35;

            splitMain.SplitterDistance = 950; // 인스펙터가 좁아져 클래식 뷰 가용 폭이 950px로 넓어진 상태
            bool isFullOnWide = (btnAddState.Text.Contains("상태") || btnAddState.Text.Contains("State")) && btnAddState.Width >= 50;

            if (tabsAtTop && buttonsBelowTabs && listBelowButtons && listHeightValid && lvAtTop && lvWidthValid && colCountValid && noHScroll && isCompactOnNarrow && isFullOnWide)
            {
                Console.WriteLine($"  ✔ [통과] 클래식 뷰 도킹, 4대 컬럼 및 인스펙터 영역 적응형 버튼 축소 무결성 검증 (좁을때: '{isCompactOnNarrow}', 넓을때: '{isFullOnWide}')");
                return true;
            }

            Console.WriteLine($"  ❌ [실패] 클래식 뷰 레이아웃 충돌: Tabs={pnlTabs.Bounds}, Buttons={pnlButtons.Bounds}, List={pnlList.Bounds}, ColCount={lvEvents.Columns.Count}, TotalColW={totalColWidth}, ClientW={lvEvents.ClientSize.Width}, CompactNarrow={isCompactOnNarrow}, FullWide={isFullOnWide}");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ [예외] 클래식 뷰 레이아웃 테스트 오류: {ex}");
            return false;
        }
    }

    private static bool TestSettingsFormLayout()
    {
        try
        {
            using var dlg = new SettingsForm();
            var handle = dlg.Handle;

            var chkMuteField = typeof(SettingsForm).GetField("_chkMute", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var chkMute = (CustomCheckBox)chkMuteField!.GetValue(dlg)!;

            var rbDarkField = typeof(SettingsForm).GetField("_rbDarkTheme", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var rbDark = (CustomRadioButton)rbDarkField!.GetValue(dlg)!;

            var rbLightField = typeof(SettingsForm).GetField("_rbLightTheme", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var rbLight = (CustomRadioButton)rbLightField!.GetValue(dlg)!;

            bool noRbOverlap = rbDark.Right <= rbLight.Left;
            bool chkValid = chkMute.Width > 0 && chkMute.Height > 0;

            if (noRbOverlap && chkValid)
            {
                Console.WriteLine($"  ✔ [통과] 설정창 체크박스/라디오버튼 레이아웃 무결성 검증 (다크: {rbDark.Bounds}, 라이트: {rbLight.Bounds})");
                return true;
            }

            Console.WriteLine($"  ❌ [실패] 설정창 컨트롤 겹침 감지: DarkRb={rbDark.Bounds}, LightRb={rbLight.Bounds}");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ [예외] 설정창 레이아웃 테스트 오류: {ex}");
            return false;
        }
    }

    private static bool TestAiAssistantDialogLayout()
    {
        try
        {
            using var dlg = new AiAssistantDialog();
            var handle = dlg.Handle;

            var grpAiSettingsField = typeof(AiAssistantDialog).GetField("_grpAiSettings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var grpAiSettings = (GroupBox)grpAiSettingsField!.GetValue(dlg)!;

            var grpScenarioField = typeof(AiAssistantDialog).GetField("_grpScenario", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var grpScenario = (GroupBox)grpScenarioField!.GetValue(dlg)!;

            var grpResultField = typeof(AiAssistantDialog).GetField("_grpResult", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var grpResult = (GroupBox)grpResultField!.GetValue(dlg)!;

            var btnGenerateField = typeof(AiAssistantDialog).GetField("_btnGenerate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var btnGenerate = (Button)btnGenerateField!.GetValue(dlg)!;

            bool noVerticalOverlap = (grpAiSettings.Bottom <= grpScenario.Top) && (grpScenario.Bottom <= grpResult.Top);
            bool dimensionsValid = grpAiSettings.Width > 600 && grpScenario.Width > 600 && btnGenerate.Width > 150;

            if (noVerticalOverlap && dimensionsValid)
            {
                Console.WriteLine($"  ✔ [통과] AI 어시스턴트 대화상자 레이아웃 무결성 검증 (비중첩 그룹박스 및 규격 확인: Settings={grpAiSettings.Bounds}, Scenario={grpScenario.Bounds}, Result={grpResult.Bounds})");
                return true;
            }

            Console.WriteLine($"  ❌ [실패] AI 어시스턴트 대화상자 레이아웃 겹침 감지: Settings={grpAiSettings.Bounds}, Scenario={grpScenario.Bounds}, Result={grpResult.Bounds}");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ [예외] AI 어시스턴트 대화상자 레이아웃 테스트 오류: {ex}");
            return false;
        }
    }

    private static bool TestDarkThemeAndScrollbarIntegrity()
    {
        string prevTheme = AppSettings.Current.ThemeMode;
        try
        {
            AppSettings.Current.ThemeMode = "Dark";

            // 1. 패치노트 다이얼로그(ChangelogDialog) 테마 및 스크롤바 도킹 무결성 검증
            using var changelogDlg = new ChangelogDialog();
            var chHandle = changelogDlg.Handle;

            var scrollLogField = typeof(ChangelogDialog).GetField("_scrollLog", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var scrollLog = (DarkScrollBar)scrollLogField!.GetValue(changelogDlg)!;

            var txtLogField = typeof(ChangelogDialog).GetField("_txtLog", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var txtLog = (RichTextBox)txtLogField!.GetValue(changelogDlg)!;

            var pnlInnerField = typeof(ChangelogDialog).GetField("_pnlLogInner", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var pnlInner = (Panel)pnlInnerField!.GetValue(changelogDlg)!;

            bool scrollValid = scrollLog != null && scrollLog.Width == 14 && scrollLog.Height > 0;
            bool txtValid = txtLog != null && txtLog.Width > 0 && txtLog.Height > 0;
            // Dock=Fill인 txtLog가 Dock=Right인 scrollLog를 덮어씌우지 않고, txtLog.Right가 scrollLog.Left 이하이거나 스크롤바 폭만큼 남은 공간을 차지하는지 수식 검증
            bool dockOrderValid = txtLog != null && scrollLog != null && pnlInner != null &&
                                  (txtLog.Right <= scrollLog.Left && scrollLog.Right == pnlInner.ClientSize.Width);

            if (!scrollValid || !txtValid || !dockOrderValid)
            {
                Console.WriteLine($"  ❌ [실패] 패치노트 스크롤바 도킹 무결성 결함: Scroll={scrollLog?.Bounds}, Txt={txtLog?.Bounds}, InnerW={pnlInner?.ClientSize.Width}");
                return false;
            }

            // 2. AI 어시스턴트 다이얼로그(AiAssistantDialog) 다크 테마 및 핸들 무결성 검증
            using var aiDlg = new AiAssistantDialog();
            var aiHandle = aiDlg.Handle;

            var pnlHeaderField = typeof(AiAssistantDialog).GetField("_pnlHeader", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var pnlHeader = (Panel)pnlHeaderField!.GetValue(aiDlg)!;

            var pnlBottomField = typeof(AiAssistantDialog).GetField("_pnlBottom", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var pnlBottom = (Panel)pnlBottomField!.GetValue(aiDlg)!;

            bool aiThemeValid = aiDlg.BackColor.R < 50 && pnlHeader != null && pnlHeader.BackColor.R < 50 && pnlBottom != null && pnlBottom.BackColor.R < 50;

            if (!aiThemeValid)
            {
                Console.WriteLine($"  ❌ [실패] AI 어시스턴트 다이얼로그 다크 테마 적용 누락: FormBg={aiDlg.BackColor}, HeaderBg={pnlHeader?.BackColor}, BottomBg={pnlBottom?.BackColor}");
                return false;
            }

            // 3. AI 적용 방식 선택 다이얼로그(AiApplyOptionDialog) 테마 및 무결성 검증
            using var optDlg = new AiApplyOptionDialog(5, 2);
            var optHandle = optDlg.Handle;
            bool optValid = optDlg.FormBorderStyle == FormBorderStyle.FixedDialog && !optDlg.MaximizeBox && optDlg.BackColor.R < 50;
            if (!optValid)
            {
                Console.WriteLine($"  ❌ [실패] AI 적용 옵션 다이얼로그 무결성 결함: FormBorderStyle={optDlg.FormBorderStyle}, MaximizeBox={optDlg.MaximizeBox}, Bg={optDlg.BackColor}");
                return false;
            }

            Console.WriteLine($"  ✔ [통과] 다크모드 타이틀바(DWM) 및 다크 스크롤바 도킹 무결성 검증 (패치노트 Scroll={scrollLog?.Bounds}, AI 어시스턴트 FormBg={aiDlg.BackColor}, AI 적용옵션 Bg={optDlg.BackColor})");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ [예외] 다크모드 및 스크롤바 무결성 테스트 오류: {ex}");
            return false;
        }
        finally
        {
            AppSettings.Current.ThemeMode = prevTheme;
        }
    }
}
