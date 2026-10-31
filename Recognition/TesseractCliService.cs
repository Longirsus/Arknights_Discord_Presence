using System.Diagnostics;
using System.Text;

namespace ArknightsPresence.Recognition;

public sealed class TesseractCliService
{
    private readonly string _executablePath;

    public TesseractCliService(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException(
                "Tesseract executable path is required.",
                nameof(executablePath));
        }

        _executablePath = executablePath;
    }

    public async Task<TesseractResult> RecognizeStageAsync(
    string imagePath,
    CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_executablePath))
        {
            throw new FileNotFoundException(
                "Could not find tesseract.exe.",
                _executablePath);
        }

        if (!File.Exists(imagePath))
        {
            throw new FileNotFoundException(
                "Could not find the OCR input image.",
                imagePath);
        }

        string processedImagePath =
            await StageImagePreprocessor.PreprocessAsync(
                imagePath,
                cancellationToken);

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _executablePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            startInfo.ArgumentList.Add(processedImagePath);
            startInfo.ArgumentList.Add("stdout");

            startInfo.ArgumentList.Add("-l");
            startInfo.ArgumentList.Add("eng");

            // Single text line worked with the processed image.
            startInfo.ArgumentList.Add("--psm");
            startInfo.ArgumentList.Add("7");

            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add(
                "tessedit_char_whitelist=0123456789-ABCDEFGHIJKLMNOPQRSTUVWXYZ");

            using var process = new Process
            {
                StartInfo = startInfo
            };

            if (!process.Start())
            {
                throw new InvalidOperationException(
                    "Tesseract failed to start.");
            }

            Task<string> outputTask =
                process.StandardOutput.ReadToEndAsync();

            Task<string> errorTask =
                process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync(cancellationToken);

            string output = await outputTask;
            string error = await errorTask;

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Tesseract exited with code {process.ExitCode}." +
                    Environment.NewLine +
                    error);
            }

            return new TesseractResult(
                RawText: output.Trim(),
                NormalizedText: NormalizeStageCode(output),
                DiagnosticText: error.Trim());
        }
        finally
        {
            // Prevent temporary OCR images from accumulating.
            if (File.Exists(processedImagePath))
            {
                File.Delete(processedImagePath);
            }
        }
    }

    private static string NormalizeStageCode(string text)
    {
        return text
            .Trim()
            .ToUpperInvariant()
            .Replace("\r", string.Empty)
            .Replace("\n", string.Empty)
            .Replace(" ", string.Empty)
            .Replace('—', '-')
            .Replace('–', '-')
            .Replace('_', '-');
    }
}

public sealed record TesseractResult(string RawText, string NormalizedText, string DiagnosticText);