using ArknightsPresence.Capture;
using ArknightsPresence.Recognition;
using DiscordRPC;
using DiscordRPC.Logging;
using System.Diagnostics;
using System.Text.RegularExpressions;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ImageSharpImage = SixLabors.ImageSharp.Image;
using ImageSharpRectangle = SixLabors.ImageSharp.Rectangle;

namespace ArknightsPresence
{
    internal static class main
    {
        private const string DiscordAppId = "1565583216281391224";
        const string tesseractPath = @"C:\Program Files\Tesseract-OCR\tesseract.exe";

        private static async Task Main()
        {
            const float cropLeft = 0.25f;
            const float cropTop = 0.45f;
            const float cropWidth = 0.50f;
            const float cropHeight = 0.15f;

            if (!File.Exists(tesseractPath))
            {
                Console.WriteLine($"Tesseract was not found:{Environment.NewLine}" + tesseractPath);
                return;
            }

            using Process? game = GameWindowFinder.FindArknights();

            if (game is null)
            {
                Console.WriteLine("Arknights was not found.");

                Console.WriteLine();
                Console.WriteLine("Detected application windows:");

                GameWindowFinder.PrintWindows();

                Console.WriteLine();
                Console.WriteLine("Use the printed process name in the finder.");

                Console.ReadLine();
                return;
            }
            game.Refresh();

            IntPtr gameWindow = game.MainWindowHandle;
            if (gameWindow == IntPtr.Zero)
            {
                Console.WriteLine("Arknights was found but no main window.");
                return;
            }

            Console.WriteLine($"Process: {game.ProcessName}");
            Console.WriteLine($"Window title: {game.MainWindowTitle}");
            Console.WriteLine($"HWND: 0x{gameWindow.ToInt64():X}");

            string captureDirectory = Path.Combine(AppContext.BaseDirectory, "Captures");

            Directory.CreateDirectory(captureDirectory);

            string fullScreenshotPath = Path.Combine(captureDirectory, "arknights-full.png");
            string cropScreenshotPath = Path.Combine(captureDirectory, "stage-region.png");

            GameWindowCapture capture = new GameWindowCapture();

            TesseractCliService tesseract = new TesseractCliService(tesseractPath);

            DiscordRpcClient discord = new DiscordRpcClient(DiscordAppId);

            discord.Logger = new ConsoleLogger(LogLevel.Info, true);

            discord.OnReady += (_, message) =>
            {
                Console.WriteLine();
                Console.WriteLine($"Connected to discord as " + $"{message.User.Username}");
            };

            discord.OnError += (_, message) =>
            {
                Console.WriteLine();
                Console.WriteLine($"Discord error: {message.Message}");
            };

            Console.WriteLine();
            Console.WriteLine("Connecting to Discord...");

            discord.Initialize();

            discord.SetPresence(CreateUnknownPresence());

            Console.WriteLine("Press Enter to capture and scan the game.");
            Console.WriteLine("Type exit and press Enter to stop.");

            using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cancellationTokenSource.Cancel();
            };

            string? lastDetectedStage = null;
            DateTime nextOcrAllowedAt = DateTime.MinValue;

            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    game.Refresh();
                    gameWindow = game.MainWindowHandle;

                    if (gameWindow == IntPtr.Zero)
                    {
                        Console.WriteLine("Arknights window is not available.");
                        await Task.Delay(TimeSpan.FromSeconds(2), cancellationTokenSource.Token);
                        continue;
                    }

                    capture.CaptureToFile(gameWindow, fullScreenshotPath);

                    await CropByPercentageAsync(fullScreenshotPath, cropScreenshotPath, cropLeft, cropTop, cropWidth, cropHeight);

                    TesseractResult result = await tesseract.RecognizeStageAsync(cropScreenshotPath);

                    string? stageCode = ExtractStageCode(result.NormalizedText);

                    if (stageCode != null && !string.Equals(stageCode, lastDetectedStage, StringComparison.OrdinalIgnoreCase))
                    {
                        lastDetectedStage = stageCode;
                        nextOcrAllowedAt = DateTime.UtcNow.AddSeconds(6);
                        Console.WriteLine($"Detected stage: {stageCode}");
                        discord.SetPresence(CreateStagePresence(stageCode));
                        Console.WriteLine("Discord Presence updated.");
                    }

                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine();
                    Console.WriteLine("Capture or OCR failed: ");
                    Console.WriteLine(ex.Message);
                }

                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(2000), cancellationTokenSource.Token);
                }
                catch (Exception OperationCanceledException) 
                {
                    break;
                }
            }
            discord.ClearPresence();
            Console.WriteLine("Rich Presence cleared");
        }

        private static async Task CropByPercentageAsync(string _sourcePath, string _outputPath, float _leftPercent, float _topPercent, float _widthPercent, float _heightPercent)
        {
            ValidatePercentage(_leftPercent, nameof(_leftPercent));
            ValidatePercentage(_topPercent, nameof(_topPercent));
            ValidatePercentage(_widthPercent, nameof(_widthPercent));
            ValidatePercentage(_heightPercent, nameof(_heightPercent));

            using SixLabors.ImageSharp.Image<Rgba32> image = await ImageSharpImage.LoadAsync<Rgba32>(_sourcePath);
            int x = (int)(image.Width * _leftPercent);
            int y = (int)(image.Height * _topPercent);
            int width = (int)(image.Width * _widthPercent);
            int height = (int)(image.Height * _heightPercent);

            x = Math.Clamp(x, 0, image.Width - 1);
            y = Math.Clamp(y, 0, image.Height - 1);
            width = Math.Min(width, image.Width - x);
            height = Math.Min(height, image.Height - y);

            if (width <= 0 || height <= 0)
            {
                throw new InvalidOperationException("The crop region is empty.");
            }

            ImageSharpRectangle cropRectangle = new ImageSharpRectangle(x, y, width, height);

            image.Mutate(context =>
            {
                context.Crop(cropRectangle);
            });

            string? outputDir = Path.GetDirectoryName(_outputPath);
            if (!string.IsNullOrWhiteSpace(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            await using FileStream outputStream = File.Create(_outputPath);

            await image.SaveAsync(outputStream, new PngEncoder());
        }

        private static void ValidatePercentage(float _value, string _parameterName)
        {
            if (_value < 0.0f || _value > 1.0f)
            {
                throw new ArgumentOutOfRangeException(_parameterName, "Percentage must be between 0 and 1.");
            }
        }

        private static string? ExtractStageCode(string _text)
        {
            if (string.IsNullOrWhiteSpace(_text))
            {
                return null;
            }

            Match match = Regex.Match(_text, @"(?<![A-Z0-9])" + @"[A-Z0-9]+" + @"(?:-[A-Z0-9]+)+" + @"(?![A-Z0-9])", RegexOptions.IgnoreCase);

            if (match.Success)
            {
                return match.Value.ToUpperInvariant();
            }
            else
            {
                return null;
            }
        }

        private static RichPresence CreateUnknownPresence()
        {
            return new RichPresence()
            {
                Details = "Arknights",
                State = "Playing Arknights",
                Timestamps = Timestamps.Now,

                Assets = new Assets
                {
                    LargeImageKey = "arknights",
                    LargeImageText = "Arknights",
                    SmallImageKey = "arknights"
                }
            };
        }

        private static RichPresence CreateStagePresence(string _stageCode)
        {
            return new RichPresence()
            {
                Details = $"Stage: {_stageCode}",
                State = "In battle",
                Timestamps = Timestamps.Now,

                Assets = new Assets
                {
                    LargeImageKey = "arknights",
                    LargeImageText = "Arknights",
                    SmallImageKey = "arknights"
                }
            };
        }

        private static readonly Timestamps SessionTimestamps = Timestamps.Now;
    }
}