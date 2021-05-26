using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Newtonsoft.Json;
using StockAnalyzer.Core.Domain;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using StockAnalyzer.Core;

namespace StockAnalyzer.CrossPlatform
{
    public class MainWindow : Window
    {
        public DataGrid Stocks => this.FindControl<DataGrid>(nameof(Stocks));
        public ProgressBar StockProgress => this.FindControl<ProgressBar>(nameof(StockProgress));
        public TextBox StockIdentifier => this.FindControl<TextBox>(nameof(StockIdentifier));
        public Button Search => this.FindControl<Button>(nameof(Search));
        public TextBox Notes => this.FindControl<TextBox>(nameof(Notes));
        public TextBlock StocksStatus => this.FindControl<TextBlock>(nameof(StocksStatus));
        public TextBlock DataProvidedBy => this.FindControl<TextBlock>(nameof(DataProvidedBy));
        public TextBlock IEX => this.FindControl<TextBlock>(nameof(IEX));
        public TextBlock IEX_Terms => this.FindControl<TextBlock>(nameof(IEX_Terms));

        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            IEX.PointerPressed += (e, a) => Open("https://iextrading.com/developer/");
            IEX_Terms.PointerPressed += (e, a) => Open("https://iextrading.com/api-exhibit-a/");

            /// Data provided for free by <a href="https://iextrading.com/developer/" RequestNavigate="Hyperlink_OnRequestNavigate">IEX</Hyperlink>. View <Hyperlink NavigateUri="https://iextrading.com/api-exhibit-a/" RequestNavigate="Hyperlink_OnRequestNavigate">IEX’s Terms of Use.</Hyperlink>
        }

        private static string API_URL = "https://ps-async.fekberg.com/api/stocks";
        private Stopwatch stopwatch = new Stopwatch();

        // This has to be 'async void', because it is an event:
        // Wrap in try-catch for safety:
        // Make sure that no code in the async-void method can throw an exception:
        private void Search_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BeforeLoadingStockData();
                
                // Represents and asynchronous operation that will return an array of strings:
                // var loadLinesTask = Task.Run<string[]>(() =>
                var loadLinesTask = Task.Run(async () =>
                {
                    using (var stream = new StreamReader(File.OpenRead("_StockPrices_Small.csv")))
                    {
                        var lines = new List<string>();

                        string line;
                        while ((line = await stream.ReadLineAsync()) != null)
                        {
                            lines.Add(line);
                        }

                        return lines;
                    }
                });

                loadLinesTask.ContinueWith(t =>
                {
                    // Will only execute if there was a problem completing the task:
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        Notes.Text = t.Exception.InnerException.Message;

                    });
                }, TaskContinuationOptions.OnlyOnFaulted);

                var processStockTask = loadLinesTask.ContinueWith((completedTask) =>
                {
                    // Task is completed here, so we can use 'Result':
                    // This will return the array of strings:
                    var lines = completedTask.Result;

                    var data = new List<StockPrice>();

                    foreach (var line in lines.Skip(1))
                    {
                        var price = StockPrice.FromCSV(line);
                        data.Add(price);
                    }

                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        Stocks.Items = data.Where(sp => sp.Identifier == StockIdentifier.Text);
                    });
                }, TaskContinuationOptions.OnlyOnRanToCompletion);

                processStockTask.ContinueWith(_ =>
                {
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        AfterLoadingStockData();
                    });
                });
            }
            catch (Exception ex)
            {
                Notes.Text = ex.Message;
            }
            finally
            {
                AfterLoadingStockData();
            }
        }

        // When exceptions are throw in 'async void', they cannot be caught:
        public async Task GetStocks()
        {
            try
            {
                var store = new DataStore();

                var responseTask = store.GetStockPrices(StockIdentifier.Text);

                Stocks.Items = await responseTask;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private void BeforeLoadingStockData()
        {
            stopwatch.Restart();
            StockProgress.IsVisible = true;
            StockProgress.IsIndeterminate = true;
        }

        private void AfterLoadingStockData()
        {
            StocksStatus.Text = $"Loaded stocks for {StockIdentifier.Text} in {stopwatch.ElapsedMilliseconds}ms";
            StockProgress.IsVisible = false;
        }

        private void Close_OnClick(object sender, RoutedEventArgs e)
        {
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                desktopLifetime.Shutdown();
            }
        }

        public static void Open(string url)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                url = url.Replace("&", "^&");
                Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
        }
    }
}
