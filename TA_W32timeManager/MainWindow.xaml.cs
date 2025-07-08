using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace W32TimeManager
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer statusTimer;
        private DispatcherTimer blinkTimer;
        private bool isLampVisible = true;
        private const string ServiceName = "W32Time";

        public MainWindow()
        {
            InitializeComponent();
            InitializeStatusTimer();
            UpdateServiceStatus();
            StartBlinking(); // ← 点滅開始
            CheckAdministratorPrivileges();
        }

        /// <summary>
        /// ランプを点滅させる処理（サービス稼働中のみ）
        /// </summary>
        private void StartBlinking()
        {
            blinkTimer = new DispatcherTimer();
            blinkTimer.Interval = TimeSpan.FromMilliseconds(200); // 点滅間隔
            blinkTimer.Tick += (s, e) =>
            {
                if (StatusText.Text.Contains("RUNNING"))
                {
                    StatusLamp.Opacity = isLampVisible ? 1.0 : 0.3;
                    isLampVisible = !isLampVisible;
                }
                else
                {
                    StatusLamp.Opacity = 1.0; // 非稼働時は点灯固定
                }
            };
            blinkTimer.Start();
        }

        /// <summary>
        /// ステータス更新タイマーの初期化
        /// </summary>
        private void InitializeStatusTimer()
        {
            statusTimer = new DispatcherTimer();
            statusTimer.Interval = TimeSpan.FromSeconds(2);
            statusTimer.Tick += (s, e) => UpdateServiceStatus();
            statusTimer.Start();
        }

        /// <summary>
        /// 管理者権限チェック
        /// </summary>
        private void CheckAdministratorPrivileges()
        {
            if (!IsRunAsAdministrator())
            {
                MessageBox.Show("このアプリケーションは管理者権限で実行する必要があります。\n" +
                              "右クリックして「管理者として実行」を選択してください。",
                              "権限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// 管理者権限で実行されているかチェック
        /// </summary>
        private bool IsRunAsAdministrator()
        {
            var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }

        /// <summary>
        /// サービスステータスの更新
        /// </summary>
        private void UpdateServiceStatus()
        {
            try
            {
                using (var service = new ServiceController(ServiceName))
                {
                    bool isRunning = service.Status == ServiceControllerStatus.Running;

                    Dispatcher.Invoke(() =>
                    {
                        if (isRunning)
                        {
                            StatusText.Text = "Status: RUNNING";
                            StatusLamp.Fill = new SolidColorBrush(Color.FromRgb(56, 161, 105)); // 緑色
                        }
                        else
                        {
                            StatusText.Text = "Status: STOP";
                            StatusLamp.Fill = new SolidColorBrush(Color.FromRgb(245, 101, 101)); // 赤色
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    StatusText.Text = "Status: ERROR";
                    StatusLamp.Fill = new SolidColorBrush(Color.FromRgb(255, 165, 0)); // オレンジ色
                });
                Debug.WriteLine($"ステータス更新エラー: {ex.Message}");
            }
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteServiceCommand("start", "W32Timeサービスを開始しています...");
        }

        private async void StopButton_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteServiceCommand("stop", "W32Timeサービスを停止しています...");
        }

        private async void StartupButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowProgress(true, "スタートアップに登録しています...");

                await Task.Run(() =>
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "sc",
                        Arguments = "config W32Time start=auto",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    using (var process = Process.Start(startInfo))
                    {
                        process.WaitForExit();
                        if (process.ExitCode != 0)
                        {
                            throw new Exception($"スタートアップ登録に失敗しました。終了コード: {process.ExitCode}");
                        }
                    }
                });

                ShowProgress(false);
                MessageBox.Show("スタートアップ登録が完了しました。", "完了",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowProgress(false);
                MessageBox.Show($"スタートアップ登録に失敗しました:\n{ex.Message}", "エラー",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ExecuteServiceCommand(string command, string progressMessage)
        {
            try
            {
                ShowProgress(true, progressMessage);

                await Task.Run(() =>
                {
                    using (var service = new ServiceController(ServiceName))
                    {
                        if (command == "start" && service.Status != ServiceControllerStatus.Running)
                        {
                            service.Start();
                            service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                        }
                        else if (command == "stop" && service.Status == ServiceControllerStatus.Running)
                        {
                            service.Stop();
                            service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                        }
                    }
                });

                ShowProgress(false);
                UpdateServiceStatus();
            }
            catch (Exception ex)
            {
                ShowProgress(false);
                MessageBox.Show($"操作に失敗しました:\n{ex.Message}", "エラー",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowProgress(bool show, string message = "")
        {
            Dispatcher.Invoke(() =>
            {
                if (show)
                {
                    ProgressBar.Visibility = Visibility.Visible;
                    ProgressBar.IsIndeterminate = true;

                    StartButton.IsEnabled = false;
                    StopButton.IsEnabled = false;
                    StartupButton.IsEnabled = false;
                }
                else
                {
                    ProgressBar.Visibility = Visibility.Collapsed;
                    ProgressBar.IsIndeterminate = false;

                    StartButton.IsEnabled = true;
                    StopButton.IsEnabled = true;
                    StartupButton.IsEnabled = true;
                }
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            statusTimer?.Stop();
            blinkTimer?.Stop(); // ← 点滅タイマーも停止
            base.OnClosed(e);
        }
    }
}
