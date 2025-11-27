using Microsoft.Win32;
using MoonsecDeobfuscator.Deobfuscation;
using MoonsecDeobfuscator.Deobfuscation.Bytecode;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using System.Threading.Tasks;

namespace MoonsecDeobfuscator
{
    public partial class MainWindow : Window
    {
        private string lastDeobfuscatedFile = "";
        private bool isWebViewReady = false;

        public MainWindow()
        {
            InitializeComponent();
            ModeBox.SelectedIndex = 0;
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await InitWebViewAsync();
        }

        private async Task InitWebViewAsync()
        {
            try
            {
                StatusText.Text = "Initializing browser...";
                RunButton.IsEnabled = false;
                string tempFolder = Path.Combine(Path.GetTempPath(), "MoonsecDeobfuscator_Temp");
                var options = new CoreWebView2EnvironmentOptions();
                var env = await CoreWebView2Environment.CreateAsync(null, tempFolder, options);

                await LuaBrowser.EnsureCoreWebView2Async(env);
                string deobfuscatedFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Deobfuscated");
                Directory.CreateDirectory(deobfuscatedFolder);
                LuaBrowser.CoreWebView2.Profile.DefaultDownloadFolderPath = deobfuscatedFolder;
                LuaBrowser.CoreWebView2.NavigationCompleted += async (s, e) =>
                {
                    if (e.IsSuccess)
                    {
                        await Task.Delay(800);
                        await Closelog();

                        if (!isWebViewReady)
                        {
                            isWebViewReady = true;
                            RunButton.IsEnabled = true;
                            StatusText.Text = "Ready. Select a file and click Run Deobfuscation.";
                        }
                    }
                };

                LuaBrowser.CoreWebView2.DownloadStarting += CoreWebView2_DownloadStarting;
                LuaBrowser.Source = new Uri("https://luadec.metaworm.site/");
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Failed to initialize: {ex.Message}";
            }
        }

        private async Task Closelog()
        {
            await LuaBrowser.CoreWebView2.ExecuteScriptAsync(@"
                (function() {
                    const closeBtn = document.querySelector('.el-dialog__headerbtn, .el-dialog__close');
                    if (closeBtn) closeBtn.click();
                    
                    const dialog = document.querySelector('.el-dialog__wrapper, .el-overlay');
                    if (dialog) dialog.remove();
                    
                    const dialogBody = document.querySelector('[id^=""el-id-""]');
                    if (dialogBody && dialogBody.closest('.el-dialog__wrapper')) {
                        dialogBody.closest('.el-dialog__wrapper').remove();
                    }
                })();
            ");
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (!string.IsNullOrEmpty(lastDeobfuscatedFile) && File.Exists(lastDeobfuscatedFile))
            {
                try { File.Delete(lastDeobfuscatedFile); } catch { }
            }
        }

        private void CoreWebView2_DownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e)
        {
            try
            {
                string toolDir = AppDomain.CurrentDomain.BaseDirectory;
                string outFolder = Path.Combine(toolDir, "Deobfuscated");
                Directory.CreateDirectory(outFolder);

                string sourceName = Path.GetFileNameWithoutExtension(e.ResultFilePath);
                string tempPath = Path.GetTempFileName();

                e.ResultFilePath = tempPath;
                e.Handled = false;

                e.DownloadOperation.StateChanged += async (s, args) =>
                {
                    var op = (CoreWebView2DownloadOperation)s;

                    if (op.State == CoreWebView2DownloadState.Completed)
                    {
                        try
                        {
                            string raw = await ReadFileWhenUnlockedAsync(tempPath);
                            string cleaned = MoonsecCleaner.Clean(raw);
                            string finalPath = Path.Combine(outFolder, sourceName + ".lua");

                            await File.WriteAllTextAsync(finalPath, cleaned);
                            if (!string.IsNullOrEmpty(lastDeobfuscatedFile) && File.Exists(lastDeobfuscatedFile))
                            {
                                try { File.Delete(lastDeobfuscatedFile); } catch { }
                            }
                            await Dispatcher.InvokeAsync(async () =>
                            {
                                StatusText.Text = $"✓ Saved → {Path.GetFileName(finalPath)}";
                                await ClearWebViewFileList();
                            });
                        }
                        catch (Exception ex)
                        {
                            Dispatcher.Invoke(() => StatusText.Text = $"Processing failed: {ex.Message}");
                        }
                        finally
                        {
                            try { File.Delete(tempPath); } catch { }
                        }
                    }
                };
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Download error: {ex.Message}";
            }
        }

        private async Task<string> ReadFileWhenUnlockedAsync(string path, int maxWaitMs = 5000)
        {
            const int delayMs = 100;
            int waited = 0;

            while (waited < maxWaitMs)
            {
                try
                {
                    return await File.ReadAllTextAsync(path);
                }
                catch (IOException)
                {
                    await Task.Delay(delayMs);
                    waited += delayMs;
                }
            }

            throw new IOException("File remained locked after waiting.");
        }

        private async Task ClearWebViewFileList()
        {
            try
            {
                await LuaBrowser.CoreWebView2.ExecuteScriptAsync(@"
                    (function() {
                        // Find and click the delete/trash button
                        const deleteBtn = Array.from(document.querySelectorAll('button, a')).find(el => {
                            const svg = el.querySelector('svg path');
                            return svg && svg.getAttribute('d') && svg.getAttribute('d').includes('M160 256H96a32');
                        });

                        if (deleteBtn) {
                            deleteBtn.click();
                            return 'CLEARED';
                        }
                        return 'NOT_FOUND';
                    })();
                ");
            }
            catch { }
        }

        private void BrowseInput(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Lua & Text Files (*.lua;*.txt)|*.lua;*.txt|All Files (*.*)|*.*",
                Title = "Select Obfuscated Lua File"
            };

            if (dialog.ShowDialog() == true)
            {
                InputPath.Text = dialog.FileName;
                InputPathDisplay.Text = Path.GetFileName(dialog.FileName);
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            }
            else
            {
                DragMove();
            }
        }

        private void Instructions_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "1. Click '▶ Run Deobfuscation'\n\n" +
                "2. Wait for the process:\n" +
                "   • Deobfuscation\n" +
                "   • Upload to decompiler\n" +
                "   • Processing\n" +
                "   • Download & clean\n\n",
                "4. Find your cleaned .lua file in the 'Deobfuscated' folder!",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void MinimizeWindow(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseWindow(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void RunCommand(object sender, RoutedEventArgs e)
        {
            string input = InputPath.Text;
            if (!File.Exists(input))
            {
                StatusText.Text = "❌ Please select a valid input file!";
                return;
            }

            string command = ((ComboBoxItem)ModeBox.SelectedItem).Tag.ToString();
            string toolDir = AppDomain.CurrentDomain.BaseDirectory;
            string nameNoExt = Path.GetFileNameWithoutExtension(input);

            try
            {
                StatusText.Text = "⚙️ Running deobfuscation...";
                var deobf = new Deobfuscator();
                var result = deobf.Deobfuscate(await File.ReadAllTextAsync(input));

                if (command == "-dev")
                {
                    string outFolder = Path.Combine(toolDir, "Deobfuscated");
                    Directory.CreateDirectory(outFolder);
                    string output = Path.Combine(outFolder, $"{nameNoExt}-Deobfuscated.luac");

                    using (var stream = new FileStream(output, FileMode.Create, FileAccess.Write))
                    {
                        var serializer = new Serializer(stream);
                        serializer.Serialize(result);
                    }
                    lastDeobfuscatedFile = output;
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    await Task.Delay(200);
                    StatusText.Text = "📤 Uploading to decompiler API...";
                    await UploadFileToWebView(output);
                }
                else
                {
                    string outFolder = Path.Combine(toolDir, "Disassembled");
                    Directory.CreateDirectory(outFolder);
                    string output = Path.Combine(outFolder, $"{nameNoExt}-Disassembled.lua");

                    string disassembly = new Disassembler(result).Disassemble();
                    await File.WriteAllTextAsync(output, disassembly);

                    StatusText.Text = $"✓ Disassembled → {Path.GetFileName(output)}";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"❌ Error: {ex.Message}";
            }
        }

        private async Task UploadFileToWebView(string filePath)
        {
            try
            {
                byte[] fileBytes = await File.ReadAllBytesAsync(filePath);
                string base64 = Convert.ToBase64String(fileBytes);
                string fileName = Path.GetFileName(filePath);

                string uploadScript = $@"
                    (async function() {{
                        const waitForElement = async (selector, maxAttempts = 50) => {{
                            for (let i = 0; i < maxAttempts; i++) {{
                                const el = document.querySelector(selector);
                                if (el) return el;
                                await new Promise(r => setTimeout(r, 200));
                            }}
                            return null;
                        }};

                        const fileInput = await waitForElement('input[type=""file""]');
                        if (!fileInput) return 'ERROR_NO_INPUT';

                        const base64Data = '{base64}';
                        const fileName = '{fileName}';
                        
                        const byteChars = atob(base64Data);
                        const byteArray = new Uint8Array([...byteChars].map(c => c.charCodeAt(0)));
                        const file = new File([byteArray], fileName, {{ type: 'application/octet-stream' }});

                        const dataTransfer = new DataTransfer();
                        dataTransfer.items.add(file);
                        fileInput.files = dataTransfer.files;

                        fileInput.dispatchEvent(new Event('change', {{ bubbles: true }}));
                        
                        return 'SUCCESS';
                    }})();
                ";

                var uploadResult = await LuaBrowser.CoreWebView2.ExecuteScriptAsync(uploadScript);

                if (uploadResult.Contains("ERROR"))
                {
                    StatusText.Text = "❌ Failed to upload file to API";
                    return;
                }

                StatusText.Text = "⏳ Processing file...";
                await WaitForProcessingAndDownload();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"❌ Upload error: {ex.Message}";
            }
        }

        private async Task WaitForProcessingAndDownload()
        {
            try
            {
                bool processingComplete = false;

                for (int i = 0; i < 120; i++)
                {
                    await Task.Delay(500);

                    var loadingCheck = await LuaBrowser.CoreWebView2.ExecuteScriptAsync(@"
                        (function() {
                            const loading = document.querySelector('.el-loading-mask');
                            const isLoading = loading && getComputedStyle(loading).display !== 'none';
                            return isLoading ? 'LOADING' : 'DONE';
                        })();
                    ");

                    if (loadingCheck.Contains("DONE"))
                    {
                        processingComplete = true;
                        break;
                    }
                    if (i % 4 == 0)
                    {
                        StatusText.Text = $"⏳ Processing... ({i / 2}s)";
                    }
                }

                if (!processingComplete)
                {
                    StatusText.Text = "⚠️ Processing timed out. Please try again.";
                    return;
                }

                StatusText.Text = "✓ Processing complete. Downloading...";
                await Task.Delay(1000);

                var downloadResult = await LuaBrowser.CoreWebView2.ExecuteScriptAsync(@"
                (function() {
                    const downloadBtn = Array.from(document.querySelectorAll('button, a')).find(el => {
                    const svgPath = el.querySelector('svg path');
                       if (!svgPath) return false;
                          const d = svgPath.getAttribute('d') || '';
                          return d.includes('V128h64v450.304z');
                   });

                if (downloadBtn) {
                   downloadBtn.click();
                   return 'CLICKED';
                }
            return 'NOT_FOUND';
            })();
        ");


                if (downloadResult.Contains("NOT_FOUND"))
                {
                    StatusText.Text = "⚠️ Download button not found. File may have failed to process.";
                    return;
                }

                StatusText.Text = "⬇️ Downloading and cleaning...";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"❌ Processing error: {ex.Message}";
            }
        }
    }
}