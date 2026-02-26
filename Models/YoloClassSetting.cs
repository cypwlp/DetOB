using System;

namespace OB.Models
{
    public class YoloClassSetting
    {
        public string Label { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public float MinConfidence { get; set; } = 0.45f;

        // --- PDF 面積觸發邏輯 ---
        public bool IsAreaSensitive { get; set; } = false;      // 是否開啟「根據 PDF 面積切換標準」
        public float PdfAreaThreshold { get; set; } = 400.0f;   // PDF 總面積閾值 (單位: mm²)

        public float MinSizeIfLargePdf { get; set; } = 3.0f;    // 當 PDF 面積 > 閾值時，目標最小尺寸要求 (mm)
        public float MinSizeIfSmallPdf { get; set; } = 1.6f;    // 當 PDF 面積 ≤ 閾值時，目標最小尺寸要求 (mm)

        // --- 固定邏輯 ---
        public float MinSizeFixed { get; set; } = 5.0f;         // 當 IsAreaSensitive 為 false 時使用的固定標準 (mm)
    }
}