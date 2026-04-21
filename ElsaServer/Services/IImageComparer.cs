using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ElsaServer.Activities; // 為了使用 VisualMatchResult 類別

namespace ElsaServer.Services;

/// <summary>
/// 影像比對服務介面
/// </summary>
public interface IImageComparer
{
    /// <summary>
    /// 比較兩張圖片位元組並計算相似度
    /// </summary>
    /// <param name="targetBytes">目標圖片</param>
    /// <param name="sourceBytes">來源圖片 (截圖)</param>
    /// <param name="threshold">信心閾值</param>
    /// <returns>回傳比對結果物件</returns>
    VisualMatchResult Compare(byte[] targetBytes, byte[] sourceBytes, double threshold);
}

/// <summary>
/// 使用 ImageSharp 進行影像比對的實作
/// </summary>
public class ImageSharpComparer : IImageComparer
{
    public VisualMatchResult Compare(byte[] targetBytes, byte[] sourceBytes, double threshold)
    {
        try
        {
            using var targetImg = Image.Load<Rgba32>(targetBytes);
            using var sourceImg = Image.Load<Rgba32>(sourceBytes);

            // 第一階段：大小必須相符
            if (targetImg.Width != sourceImg.Width || targetImg.Height != sourceImg.Height)
            {
                return new VisualMatchResult { IsMatch = false, Confidence = 0 };
            }

            int diffPixels = 0;
            int totalPixels = targetImg.Width * targetImg.Height;

            for (int y = 0; y < targetImg.Height; y++)
            {
                for (int x = 0; x < targetImg.Width; x++)
                {
                    var p1 = targetImg[x, y];
                    var p2 = sourceImg[x, y];

                    // 簡單的 RGB 差異
                    int diff = Math.Abs(p1.R - p2.R) + Math.Abs(p1.G - p2.G) + Math.Abs(p1.B - p2.B);
                    if (diff > 30) // 容忍小幅顏色誤差
                    {
                        diffPixels++;
                    }
                }
            }

            double similarity = 1.0 - ((double)diffPixels / totalPixels);
            bool isMatch = similarity >= threshold;

            return new VisualMatchResult
            {
                IsMatch = isMatch,
                Confidence = similarity,
                MatchX = 0,
                MatchY = 0
            };
        }
        catch (Exception)
        {
            return new VisualMatchResult { IsMatch = false, Confidence = 0 };
        }
    }
}
