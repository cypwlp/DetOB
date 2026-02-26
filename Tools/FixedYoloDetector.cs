using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;
using Docnet.Core;
using Docnet.Core.Models;
using OB.Models;

namespace OB.Tools
{
    public class FixedYoloDetector : IDisposable
    {
        private InferenceSession? _session;
        private string[]? _labels;
        private int _inputWidth;
        private int _inputHeight;
        private string _inputName = "images";

        // 1 point = 25.4 / 72 mm
        private const double PointToMm = 0.3527777778;

        public async Task LoadDefaultModelAsync()
        {
            string modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OnnxModel", "yolo11s_fixed.onnx");
            if (!File.Exists(modelPath)) throw new FileNotFoundException($"模型丟失: {modelPath}");

            await Task.Run(() => {
                _session = new InferenceSession(modelPath);
                var inputMeta = _session.InputMetadata.First();
                _inputName = inputMeta.Key;
                _inputWidth = inputMeta.Value.Dimensions[3];
                _inputHeight = inputMeta.Value.Dimensions[2];

                var metadata = _session.ModelMetadata.CustomMetadataMap;
                if (metadata.TryGetValue("names", out string? namesJson))
                    _labels = ParseYoloLabels(namesJson);
            });
        }

        public async Task<List<Prediction>> DetectPdfAsync(string pdfPath, int pageIndex = 0, double scaling = 2.5d)
        {
            if (_session == null) await LoadDefaultModelAsync();

            // 讀取配置字典
            var configDict = new Dictionary<string, YoloClassSetting>();
            if (!string.IsNullOrEmpty(OB.Default.mYoloLabels))
            {
                var list = JsonSerializer.Deserialize<List<YoloClassSetting>>(OB.Default.mYoloLabels);
                if (list != null) configDict = list.ToDictionary(x => x.Label, x => x);
            }

            return await Task.Run(() =>
            {
                // 1. 獲取 PDF 物理面積信息 (單位轉換為 mm)
                double totalPdfAreaMm2 = 0;
                double rawPointsWidth, rawPointsHeight;
                using (var library = DocLib.Instance)
                {
                    using var docReader = library.GetDocReader(pdfPath, new PageDimensions(1.0d));
                    using var pageReader = docReader.GetPageReader(pageIndex);
                    rawPointsWidth = pageReader.GetPageWidth();
                    rawPointsHeight = pageReader.GetPageHeight();
                }

                double pdfWidthMm = rawPointsWidth * PointToMm;
                double pdfHeightMm = rawPointsHeight * PointToMm;
                totalPdfAreaMm2 = pdfWidthMm * pdfHeightMm; // 這裡是 mm²

                // 2. 渲染圖片
                using var libraryRender = DocLib.Instance;
                using var docReaderRender = libraryRender.GetDocReader(pdfPath, new PageDimensions(scaling));
                using var pageReaderRender = docReaderRender.GetPageReader(pageIndex);
                int renderWidth = pageReaderRender.GetPageWidth();
                int renderHeight = pageReaderRender.GetPageHeight();
                byte[] rawBgrBytes = pageReaderRender.GetImage();

                double pixelsPerMm = renderWidth / pdfWidthMm;

                using var fullImage = new SKBitmap();
                var info = new SKImageInfo(renderWidth, renderHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
                GCHandle handle = GCHandle.Alloc(rawBgrBytes, GCHandleType.Pinned);
                fullImage.InstallPixels(info, handle.AddrOfPinnedObject(), info.RowBytes, (addr, ctx) => { handle.Free(); });

                // 3. 執行偵測 (切塊處理)
                var allPredictions = new List<Prediction>();
                int tileSize = _inputWidth;
                int step = (int)(tileSize * 0.75);
                for (int y = 0; y < renderHeight; y += step)
                {
                    for (int x = 0; x < renderWidth; x += step)
                    {
                        int currX = Math.Min(x, renderWidth - tileSize);
                        int currY = Math.Min(y, renderHeight - tileSize);
                        using var tile = new SKBitmap(tileSize, tileSize);
                        fullImage.ExtractSubset(tile, SKRectI.Create(currX, currY, tileSize, tileSize));
                        var tileResults = PerformInference(tile, 0.15f);
                        foreach (var res in tileResults)
                        {
                            res.Box = new SKRect(res.Box.Left + currX, res.Box.Top + currY, res.Box.Right + currX, res.Box.Bottom + currY);
                            allPredictions.Add(res);
                        }
                        if (x + tileSize >= renderWidth) break;
                    }
                    if (y + tileSize >= renderHeight) break;
                }

                // 4. NMS 與 判定邏輯
                var finalResults = ApplyNMS(allPredictions, 0.45f);
                var validatedResults = new List<Prediction>();

                foreach (var pred in finalResults)
                {
                    if (!configDict.TryGetValue(pred.Label, out var setting) || !setting.IsEnabled) continue;

                    // 目標物理尺寸 (mm)
                    pred.RealWidthMm = (float)(pred.Box.Width / pixelsPerMm);
                    pred.RealHeightMm = (float)(pred.Box.Height / pixelsPerMm);

                    // --- 決定判定標準 ---
                    float requiredMinSize;
                    if (setting.IsAreaSensitive)
                    {
                        // 根據 PDF 總面積 (mm²) 決定使用哪個標準
                        requiredMinSize = (totalPdfAreaMm2 > setting.PdfAreaThreshold)
                                          ? setting.MinSizeIfLargePdf
                                          : setting.MinSizeIfSmallPdf;
                    }
                    else
                    {
                        // 如果對 PDF 面積不敏感，則跳過上述判斷，使用固定標準
                        requiredMinSize = setting.MinSizeFixed;
                    }

                    // --- 執行合格性檢查 ---
                    bool isConfOk = pred.Confidence >= setting.MinConfidence;
                    bool isSizeOk = (pred.RealWidthMm >= requiredMinSize && pred.RealHeightMm >= requiredMinSize);

                    if (!isConfOk || !isSizeOk)
                    {
                        pred.IsQualified = false;
                        var errorList = new List<string>();
                        if (!isConfOk) errorList.Add($"置信度低({pred.Confidence:P0})");
                        if (!isSizeOk) errorList.Add($"尺寸不達標(需{requiredMinSize}mm, 實測{pred.RealWidthMm:F1}x{pred.RealHeightMm:F1})");
                        pred.FailReason = string.Join(" | ", errorList);
                    }

                    validatedResults.Add(pred);
                }
                return validatedResults;
            });
        }

        private List<Prediction> PerformInference(SKBitmap bitmap, float threshold)
        {
            var tensor = BitmapToTensor(bitmap);
            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(_inputName, tensor) };
            using var results = _session!.Run(inputs);
            var output = results.First().AsTensor<float>();

            var list = new List<Prediction>();
            int numClasses = output.Dimensions[1] - 4;
            int numAnchors = output.Dimensions[2];
            for (int i = 0; i < numAnchors; i++)
            {
                float maxConf = 0; int labelId = -1;
                for (int j = 0; j < numClasses; j++)
                {
                    float conf = output[0, j + 4, i];
                    if (conf > maxConf) { maxConf = conf; labelId = j; }
                }
                if (maxConf >= threshold)
                {
                    float cx = output[0, 0, i], cy = output[0, 1, i], w = output[0, 2, i], h = output[0, 3, i];
                    list.Add(new Prediction
                    {
                        Box = new SKRect(cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2),
                        Label = (_labels != null && labelId < _labels.Length) ? _labels[labelId] : labelId.ToString(),
                        Confidence = maxConf
                    });
                }
            }
            return list;
        }

        private unsafe DenseTensor<float> BitmapToTensor(SKBitmap bitmap)
        {
            var tensor = new DenseTensor<float>(new[] { 1, 3, _inputHeight, _inputWidth });
            byte* ptr = (byte*)bitmap.GetPixels().ToPointer();
            for (int y = 0; y < _inputHeight; y++)
            {
                byte* row = ptr + (y * bitmap.RowBytes);
                for (int x = 0; x < _inputWidth; x++)
                {
                    tensor[0, 0, y, x] = row[x * 4 + 2] / 255.0f; // R
                    tensor[0, 1, y, x] = row[x * 4 + 1] / 255.0f; // G
                    tensor[0, 2, y, x] = row[x * 4 + 0] / 255.0f; // B
                }
            }
            return tensor;
        }

        private List<Prediction> ApplyNMS(List<Prediction> predictions, float iouThreshold)
        {
            var sorted = predictions.OrderByDescending(p => p.Confidence).ToList();
            var result = new List<Prediction>();
            while (sorted.Count > 0)
            {
                var current = sorted[0]; result.Add(current); sorted.RemoveAt(0);
                for (int i = sorted.Count - 1; i >= 0; i--)
                {
                    if (CalculateIoU(current.Box, sorted[i].Box) > iouThreshold) sorted.RemoveAt(i);
                }
            }
            return result;
        }

        private float CalculateIoU(SKRect r1, SKRect r2)
        {
            if (!r1.IntersectsWith(r2)) return 0;
            var intersect = SKRect.Intersect(r1, r2);
            float areaI = intersect.Width * intersect.Height;
            float areaU = (r1.Width * r1.Height) + (r2.Width * r2.Height) - areaI;
            return areaI / areaU;
        }

        private string[] ParseYoloLabels(string json)
        {
            try { return json.Trim('{', '}').Split(',').Select(x => x.Split(':')[1].Trim(' ', '\'', '\"')).ToArray(); }
            catch { return Array.Empty<string>(); }
        }

        public void Dispose() => _session?.Dispose();
    }
}