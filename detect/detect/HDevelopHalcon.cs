using HalconDotNet;
using Microsoft.VisualBasic;
using System;
using System.IO;
using System.Windows.Forms;

namespace YourNamespace
{
    public class HDevelopHalcon : IDisposable
    {
        private bool _disposed = false;
        private HTuple modelId = null;
        private HImage templateImage = null;

        private HTuple templateWidth = 0;
        private HTuple templateHeight = 0;

        // 缓存匹配阈值
        private double? minScoreCache = null;

        /// <summary>
        /// 自适应显示图像
        /// </summary>
        /// <summary>
        /// 按照当前窗口宽高比显示图像，保持比例
        /// </summary>
        public void DispImageAdaptive(HWindowControl hWinControl, HImage image, int imgWidth, int imgHeight)
        {
            try
            {
                HWindow window = hWinControl.HalconWindow;
                window.ClearWindow();

                double winWidth = hWinControl.Width;
                double winHeight = hWinControl.Height;
                double winRatio = winWidth / winHeight;
                double imgRatio = (double)imgWidth / imgHeight;

                int newRow1, newCol1, newRow2, newCol2;

                if (winRatio > imgRatio)
                {
                    double dispHeight = imgHeight;
                    double dispWidth = dispHeight * winRatio;
                    double deltaW = (dispWidth - imgWidth) / 2;
                    newRow1 = 0;
                    newRow2 = imgHeight - 1;
                    newCol1 = (int)(-deltaW);
                    newCol2 = (int)(imgWidth - 1 + deltaW);
                }
                else
                {
                    double dispWidth = imgWidth;
                    double dispHeight = dispWidth / winRatio;
                    double deltaH = (dispHeight - imgHeight) / 2;
                    newCol1 = 0;
                    newCol2 = imgWidth - 1;
                    newRow1 = (int)(-deltaH);
                    newRow2 = (int)(imgHeight - 1 + deltaH);
                }

                window.SetPart(newRow1, newCol1, newRow2, newCol2);
                window.DispObj(image);
            }
            catch
            {
                try
                {
                    hWinControl.HalconWindow.ClearWindow();
                    hWinControl.HalconWindow.DispObj(image);
                }
                catch
                {
                    hWinControl.HalconWindow.ClearWindow();
                }
            }
        }


        /// <summary>
        /// 在当前图像上交互式创建模板
        /// </summary>
        public void CreateTemplateInteractive(HWindowControl hWin, string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                throw new FileNotFoundException("图像不存在: " + imagePath);

            templateImage?.Dispose();
            templateImage = new HImage(imagePath);

            // ===== 保存当前 SetPart =====
            HTuple row1, col1, row2, col2;
            hWin.HalconWindow.GetPart(out row1, out col1, out row2, out col2);

            // 显示图像（保持原有比例，不使用 DispImageAdaptive）
            templateImage.DispObj(hWin.HalconWindow);

            HTuple row, col, phi, len1, len2;
            HOperatorSet.DrawRectangle2(hWin.HalconWindow, out row, out col, out phi, out len1, out len2);

            templateHeight = len1 * 2;
            templateWidth = len2 * 2;

            HObject ho_Rect;
            HOperatorSet.GenRectangle2(out ho_Rect, row, col, phi, len1, len2);

            HObject ho_Template;
            HOperatorSet.ReduceDomain(templateImage, ho_Rect, out ho_Template);

            if (modelId != null)
                HOperatorSet.ClearShapeModel(modelId);

            HOperatorSet.CreateShapeModel(ho_Template,
                8,
                (new HTuple(0)).TupleRad(),
                (new HTuple(360)).TupleRad(),
                (new HTuple(0.091)).TupleRad(),
                (new HTuple("point_reduction_high")).TupleConcat("no_pregeneration"),
                "use_polarity",
                ((new HTuple(39)).TupleConcat(67)).TupleConcat(121),
                3,
                out modelId);

            ho_Template.Dispose();
            ho_Rect.Dispose();

            HObject ho_ModelContours;
            HOperatorSet.GetShapeModelContours(out ho_ModelContours, modelId, 1);

            hWin.HalconWindow.SetColor("green");
            hWin.HalconWindow.DispObj(ho_ModelContours);
            ho_ModelContours.Dispose();

            // ===== 恢复 SetPart，使图片比例不变 =====
            hWin.HalconWindow.SetPart(row1, col1, row2, col2);

            MessageBox.Show("模板创建完成！", "提示");
        }


        /// <summary>
        /// 单张图像模板匹配
        /// </summary>
        // 在 HDevelopHalcon 类里
        private double matchThreshold = 0.5;
        private bool thresholdSet = false;
        public void MatchSingleImage(HWindowControl hWin, HImage img, ref double minScore)
        {
            if (modelId == null)
            {
                MessageBox.Show("请先创建模板！", "提示");
                return;
            }

            try
            {
                // 第一次调用时，如果 minScore 未设置，则弹窗
                if (minScore < 0)
                {
                    string input = Interaction.InputBox("请输入匹配分数阈值（0~1之间，如0.5）:", "输入阈值", "0.5");
                    if (!double.TryParse(input, out double val) || val < 0 || val > 1)
                    {
                        MessageBox.Show("输入阈值无效！", "错误");
                        return;
                    }
                    minScore = val;
                }

                HTuple row, col, angle, score;
                HOperatorSet.FindShapeModel(img, modelId, 0, new HTuple(360).TupleRad(),
                                            minScore, 1, 0.5, "least_squares", 0, 0.9,
                                            out row, out col, out angle, out score);

                // 显示图像
                // 获取图片宽高
                HTuple w, h;
                img.GetImageSize(out w, out h);
                DispImageAdaptive(hWin, img, w.I, h.I);


                if (score.Length > 0)
                {
                    // 获取模板轮廓
                    HObject ho_ModelContours;
                    HOperatorSet.GetShapeModelContours(out ho_ModelContours, modelId, 1);

                    for (int i = 0; i < score.Length; i++)
                    {
                        // 仿射变换轮廓到匹配位置
                        HObject ho_TransContours;
                        HTuple homMat2D;
                        HOperatorSet.HomMat2dIdentity(out homMat2D);
                        HOperatorSet.HomMat2dRotate(homMat2D, angle[i], 0, 0, out homMat2D);
                        HOperatorSet.HomMat2dTranslate(homMat2D, row[i], col[i], out homMat2D);
                        HOperatorSet.AffineTransContourXld(ho_ModelContours, out ho_TransContours, homMat2D);

                        // 显示匹配轮廓
                        hWin.HalconWindow.SetColor("green");
                        hWin.HalconWindow.SetLineWidth(2);
                        hWin.HalconWindow.DispObj(ho_TransContours);
                        ho_TransContours.Dispose();
                    }
                    ho_ModelContours.Dispose();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"模板匹配失败: {ex.Message}");
            }
        }


        /// <summary>
        /// 设置匹配阈值
        /// </summary>
        public void SetMinScore(double score)
        {
            if (score < 0 || score > 1)
                throw new ArgumentOutOfRangeException("score", "阈值必须在 0~1 之间");
            minScoreCache = score;
        }

        /// <summary>
        /// 获取当前阈值
        /// </summary>
        public double? GetMinScore()
        {
            return minScoreCache;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (modelId != null)
                {
                    HOperatorSet.ClearShapeModel(modelId);
                    modelId = null;
                }
                templateImage?.Dispose();
                _disposed = true;
            }
        }
    }
}