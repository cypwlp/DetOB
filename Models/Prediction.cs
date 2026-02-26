using SkiaSharp;

namespace OB.Models
{
    public class Prediction
    {
        public SKRect Box { get; set; }          // 像素座標
        public string Label { get; set; }        // 類別名稱
        public float Confidence { get; set; }     // 置信度

        public float RealWidthMm { get; set; }   // 物理寬度 (mm)
        public float RealHeightMm { get; set; }  // 物理高度 (mm)

        public bool IsQualified { get; set; } = true;
        public string FailReason { get; set; } = string.Empty;
    }
}