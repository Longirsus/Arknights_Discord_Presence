using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ArknightsPresence.Recognition;

public static class StageImagePreprocessor
{
    public static async Task<string> PreprocessAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Source image was not found.", sourcePath);
        }

        string outputPath = Path.Combine(Path.GetTempPath(), $"arknights-ocr-{Guid.NewGuid():N}.png");

        using Image<L8> image = await Image.LoadAsync<L8>( sourcePath, cancellationToken);

        image.Mutate(context =>
        {
            context.Resize(image.Width * 3, image.Height * 3);
            context.BinaryThreshold(0.78f);

            // Give Tesseract empty space around the text.
            context.Pad( image.Width + 120, image.Height + 120, Color.Black);
        });

        await image.SaveAsPngAsync(outputPath, cancellationToken);

        return outputPath;
    }
}