using HalconDotNet;
using System;

namespace YourNamespace
{
    public class HalconBlobHelper : IDisposable
    {
        public void DispMessage(HTuple windowHandle, string message, string coordSystem = "window", double row = 12, double col = 12, string color = "black", string box = "true")
        {
            HOperatorSet.DispText(windowHandle, message, coordSystem, row, col, color, new HTuple(), new HTuple());
        }

        public HObject ProcessBlob(HObject image, HTuple windowHandle, int minGray = 160, int maxGray = 255, double minArea = 50000)
        {
            HObject roi, imageReduced, region, regionClosed, connectedRegions, selectedRegions;

            // 交互选择 ROI
            HTuple row, col, phi, length1, length2;
            HalconDotNet.HOperatorSet.DrawRectangle2(windowHandle, out row, out col, out phi, out length1, out length2);
            HalconDotNet.HOperatorSet.GenRectangle2(out roi, row, col, phi, length1, length2);

            // 限制域
            HalconDotNet.HOperatorSet.ReduceDomain(image, roi, out imageReduced);

            // 阈值分割
            HalconDotNet.HOperatorSet.Threshold(imageReduced, out region, minGray, maxGray);

            // 闭操作
            HalconDotNet.HOperatorSet.ClosingCircle(region, out regionClosed, 3.5);

            // 连接区域
            HalconDotNet.HOperatorSet.Connection(regionClosed, out connectedRegions);

            // 筛选大区域
            HalconDotNet.HOperatorSet.SelectShape(connectedRegions, out selectedRegions, "area", "and", minArea, 99999999);

            // 显示结果
            HalconDotNet.HOperatorSet.SetDraw(windowHandle, "fill");
            HalconDotNet.HOperatorSet.SetColor(windowHandle, "green");
            HalconDotNet.HOperatorSet.DispObj(selectedRegions, windowHandle);

            // 释放临时对象
            roi.Dispose();
            imageReduced.Dispose();
            region.Dispose();
            regionClosed.Dispose();
            connectedRegions.Dispose();

            return selectedRegions;
        }

        public void Dispose()
        {
            // 如果以后需要释放资源，可以在这里实现
        }
    }
}
