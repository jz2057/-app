using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using HalconDotNet;
using Microsoft.VisualBasic;

namespace YourNamespace
{
    public partial class Form1 : Form
    {
        private HWindowControl hWin;
        private HDevelopHalcon halcon;

        private class Flow
        {
            public string FilePath { get; set; }
            public string FileName => System.IO.Path.GetFileName(FilePath);
            public HImage Image { get; set; }
        }
        private List<Flow> flows = new List<Flow>();
        private int currentIndex = 0;
        private string templateImagePath;
        private bool templateCreated = false;
        private double minScore = -1; // 模板匹配阈值，-1表示未设置

        private ComboBox cbImageSelector;
        private Button btnPrev, btnNext;
        private Button btnResetView;

        private Point lastMousePos = Point.Empty;
        private bool isDragging = false;
        private double zoomFactor = 1.0;
        private int imageWidth = 0;
        private int imageHeight = 0;

        private int blobMinGray = 160;
        private int blobMaxGray = 255;
        private int blobMinArea = 50000;

        private bool blobParamSet = false;
        private bool blobAnalysisEnabled = false; // 是否显示Blob

        public Form1()
        {
            InitializeComponent();
            halcon = new HDevelopHalcon();
            InitHalconWindow();
            InitUI();

            // 窗口大小变化时自动调整显示
            this.Resize += Form1_Resize;
        }

        private void InitHalconWindow()
        {
            hWin = new HWindowControl();
            hWin.Dock = DockStyle.Fill;
            hWin.HMouseDown += HWin_HMouseDown;
            hWin.HMouseUp += HWin_HMouseUp;
            hWin.HMouseMove += HWin_HMouseMove;
            //hWin.HMouseWheel += HWin_HMouseWheel;
            this.Controls.Add(hWin);
        }

        private void InitUI()
        {
            FlowLayoutPanel topPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(5),
                AutoScroll = true
            };
            this.Controls.Add(topPanel);

            Button btnImportImages = new Button { Text = "图片导入", AutoSize = true, Margin = new Padding(5) };
            btnImportImages.Click += BtnImportImages_Click;
            topPanel.Controls.Add(btnImportImages);

            cbImageSelector = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150, Margin = new Padding(5) };
            cbImageSelector.SelectedIndexChanged += CbImageSelector_SelectedIndexChanged;
            topPanel.Controls.Add(cbImageSelector);

            // 在当前图创建模板按钮
            Button btnCreateTemplate = new Button { Text = "在当前图创建模板", AutoSize = true, Margin = new Padding(5) };
            btnCreateTemplate.Click += BtnCreateTemplate_Click;
            topPanel.Controls.Add(btnCreateTemplate);

            // 取消模板匹配按钮（紧挨创建模板按钮）
            Button btnCancelTemplate = new Button { Text = "取消模板匹配", AutoSize = true, Margin = new Padding(5) };
            btnCancelTemplate.Click += BtnCancelTemplate_Click;
            topPanel.Controls.Add(btnCancelTemplate);

            // 修改匹配阈值按钮
            Button btnChangeThreshold = new Button { Text = "修改匹配阈值", AutoSize = true, Margin = new Padding(5) };
            btnChangeThreshold.Click += BtnChangeThreshold_Click;
            topPanel.Controls.Add(btnChangeThreshold);

            // Blob 分析按钮
            Button btnBlobProcess = new Button { Text = "Blob 分析", AutoSize = true, Margin = new Padding(5) };
            btnBlobProcess.Click += BtnBlobProcess_Click;
            topPanel.Controls.Add(btnBlobProcess);

            // 取消Blob按钮
            Button btnCancelBlob = new Button { Text = "取消Blob", AutoSize = true, Margin = new Padding(5) };
            btnCancelBlob.Click += BtnCancelBlob_Click;
            topPanel.Controls.Add(btnCancelBlob);

            btnPrev = new Button { Text = "上一张", AutoSize = true, Margin = new Padding(5) };
            btnPrev.Click += BtnPrev_Click;
            topPanel.Controls.Add(btnPrev);

            btnNext = new Button { Text = "下一张", AutoSize = true, Margin = new Padding(5) };
            btnNext.Click += BtnNext_Click;
            topPanel.Controls.Add(btnNext);

            btnResetView = new Button { Text = "重置视图", AutoSize = true, Margin = new Padding(5) };
            btnResetView.Click += BtnResetView_Click;
            topPanel.Controls.Add(btnResetView);
        }


        #region 鼠标事件
        private void HWin_HMouseDown(object sender, HMouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isDragging = true;
                lastMousePos = new Point((int)e.X, (int)e.Y);
                hWin.Cursor = Cursors.Hand;
            }
        }

        private void HWin_HMouseMove(object sender, HMouseEventArgs e)
        {
            if (isDragging)
            {
                int deltaX = (int)e.X - lastMousePos.X;
                int deltaY = (int)e.Y - lastMousePos.Y;
                lastMousePos = new Point((int)e.X, (int)e.Y);
                MoveImage(deltaX, deltaY);
            }
        }

        private void HWin_HMouseUp(object sender, HMouseEventArgs e)
        {
            if (isDragging && e.Button == MouseButtons.Left)
            {
                isDragging = false;
                hWin.Cursor = Cursors.Default;
            }
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_MOUSEWHEEL = 0x020A;
            if (m.Msg == WM_MOUSEWHEEL)
            {
                // 将鼠标位置转换到 hWin 客户区
                Point mousePos = hWin.PointToClient(Cursor.Position);
                if (hWin.ClientRectangle.Contains(mousePos))
                {
                    long wParam = m.WParam.ToInt64();
                    int delta = (short)((wParam >> 16) & 0xFFFF);

                    float oldZoom = (float)zoomFactor;
                    if (oldZoom <= 0) oldZoom = 1.0f;

                    // 根据滚轮方向缩放
                    zoomFactor *= (delta > 0) ? 1.1f : 0.9f;
                    if (zoomFactor < 0.1f) zoomFactor = 0.1f;
                    if (zoomFactor > 20.0f) zoomFactor = 20.0f;

                    // 缩放 Halcon 图像
                    ZoomImage((double)(zoomFactor / oldZoom), mousePos.X, mousePos.Y);
                    return; // 拦截消息
                }
            }
            base.WndProc(ref m);
        }

        #endregion

        #region 拖拽和缩放
        private void MoveImage(int deltaX, int deltaY)
        {
            try
            {
                HTuple row1, col1, row2, col2;
                hWin.HalconWindow.GetPart(out row1, out col1, out row2, out col2);

                double displayWidth = col2.D - col1.D;
                double displayHeight = row2.D - row1.D;

                int winWidth = hWin.Width;
                int winHeight = hWin.Height;

                double colScale = displayWidth / winWidth;
                double rowScale = displayHeight / winHeight;

                int newCol1 = (int)(col1.D - deltaX * colScale);
                int newRow1 = (int)(row1.D - deltaY * rowScale);
                int newCol2 = newCol1 + (int)displayWidth;
                int newRow2 = newRow1 + (int)displayHeight;

                newCol1 = Math.Max(0, Math.Min(newCol1, imageWidth - 1));
                newRow1 = Math.Max(0, Math.Min(newRow1, imageHeight - 1));
                newCol2 = Math.Max(newCol1 + 1, Math.Min(newCol2, imageWidth));
                newRow2 = Math.Max(newRow1 + 1, Math.Min(newRow2, imageHeight));

                hWin.HalconWindow.SetPart(newRow1, newCol1, newRow2, newCol2);
                RedisplayImage();
            }
            catch (Exception ex)
            {
                Console.WriteLine("移动图像错误: " + ex.Message);
            }
        }

        private void ZoomImage(double factor, int centerX, int centerY)
        {
            try
            {
                HTuple row1, col1, row2, col2;
                hWin.HalconWindow.GetPart(out row1, out col1, out row2, out col2);

                double displayWidth = col2.D - col1.D;
                double displayHeight = row2.D - row1.D;

                int winWidth = hWin.Width;
                int winHeight = hWin.Height;

                double colScale = displayWidth / winWidth;
                double rowScale = displayHeight / winHeight;

                double mouseCol = col1.D + centerX * colScale;
                double mouseRow = row1.D + centerY * rowScale;

                double newDisplayWidth = displayWidth / factor;
                double newDisplayHeight = displayHeight / factor;

                double newCol1 = mouseCol - (centerX / (double)winWidth) * newDisplayWidth;
                double newRow1 = mouseRow - (centerY / (double)winHeight) * newDisplayHeight;
                double newCol2 = newCol1 + newDisplayWidth;
                double newRow2 = newRow1 + newDisplayHeight;

                newCol1 = Math.Max(0, newCol1);
                newRow1 = Math.Max(0, newRow1);
                newCol2 = Math.Min(imageWidth, newCol2);
                newRow2 = Math.Min(imageHeight, newRow2);

                zoomFactor = displayWidth / newDisplayWidth;

                hWin.HalconWindow.SetPart((int)newRow1, (int)newCol1, (int)newRow2, (int)newCol2);
                RedisplayImage();
            }
            catch (Exception ex)
            {
                Console.WriteLine("缩放图像错误: " + ex.Message);
            }
        }
        #endregion

        #region 图片显示和重置
        private void RedisplayImage()
        {
            if (flows.Count == 0 || currentIndex < 0 || currentIndex >= flows.Count) return;

            try
            {
                hWin.HalconWindow.ClearWindow();
                HImage img = flows[currentIndex].Image;

                // 模板匹配优先显示
                if (templateCreated)
                    halcon.MatchSingleImage(hWin, img, ref minScore);
                else
                    img.DispObj(hWin.HalconWindow);   

                // 如果启用Blob分析，再叠加显示Blob
                if (blobAnalysisEnabled)
                {
                    HObject region, regionClosed, connectedRegions, selectedRegions;
                    HOperatorSet.Threshold(img, out region, blobMinGray, blobMaxGray);
                    HOperatorSet.ClosingCircle(region, out regionClosed, 3.5);
                    HOperatorSet.Connection(regionClosed, out connectedRegions);
                    HOperatorSet.SelectShape(connectedRegions, out selectedRegions, "area", "and", blobMinArea, 99999999);

                    HOperatorSet.SetColor(hWin.HalconWindow, "green");
                    HOperatorSet.SetDraw(hWin.HalconWindow, "fill");
                    HOperatorSet.DispObj(selectedRegions, hWin.HalconWindow);

                    HTuple numObjects;
                    HOperatorSet.CountObj(selectedRegions, out numObjects);
                    HOperatorSet.DispText(
                        hWin.HalconWindow,
                        numObjects.I > 0 ? $"检测到目标：{numObjects.I} 个,是反面" : "未检测到目标，是正面",
                        "window", 40, 40, "red", new HTuple(), new HTuple()
                    );

                    region.Dispose();
                    regionClosed.Dispose();
                    connectedRegions.Dispose();
                    selectedRegions.Dispose();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("重显示图像错误: " + ex.Message);
            }
        }

        private void ResetView()
        {
            if (flows.Count == 0 || currentIndex < 0 || currentIndex >= flows.Count) return;

            try
            {
                HTuple width, height;
                flows[currentIndex].Image.GetImageSize(out width, out height);
                imageWidth = width.I;
                imageHeight = height.I;

                // 保证初始显示完整图像
                FitImageToWindow();

                zoomFactor = 1.0;

                RedisplayImage();
            }
            catch (Exception ex)
            {
                Console.WriteLine("重置视图错误: " + ex.Message);
            }
        }
        #endregion

        #region 图片导入与选择
        private void BtnImportImages_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Filter = "Image Files|*.bmp;*.jpg;*.png";
                dlg.Multiselect = true;

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    flows.Clear();
                    cbImageSelector.Items.Clear();

                    foreach (var file in dlg.FileNames)
                    {
                        HImage img = new HImage(file);
                        flows.Add(new Flow { FilePath = file, Image = img });
                        cbImageSelector.Items.Add(System.IO.Path.GetFileName(file));
                    }

                    if (flows.Count > 0)
                    {
                        currentIndex = 0;
                        cbImageSelector.SelectedIndex = 0;
                        ResetView();
                    }
                }
            }
        }

        private void CbImageSelector_SelectedIndexChanged(object sender, EventArgs e)
        {
            int index = cbImageSelector.SelectedIndex;
            if (index >= 0 && index < flows.Count)
            {
                currentIndex = index;
                RedisplayImage();
            }
        }
        #endregion

        #region 模板操作与翻页
        private void BtnCreateTemplate_Click(object sender, EventArgs e)
        {
            if (flows.Count == 0)
            {
                MessageBox.Show("请先导入图片！", "提示");
                return;
            }

            templateImagePath = flows[currentIndex].FilePath;
            halcon.CreateTemplateInteractive(hWin, templateImagePath);
            templateCreated = true;

            if (minScore < 0) // 第一次创建模板时设置阈值
            {
                string input = Interaction.InputBox("请输入匹配分数阈值（0~1之间，如0.5）:", "输入阈值", "0.5");
                if (double.TryParse(input, out double val) && val >= 0 && val <= 1)
                    minScore = val;
            }

            ResetView();
        }

        private void BtnChangeThreshold_Click(object sender, EventArgs e)
        {
            string input = Interaction.InputBox("请输入匹配分数阈值（0~1之间，如0.5）:", "修改阈值", minScore >= 0 ? minScore.ToString() : "0.5");
            if (double.TryParse(input, out double val) && val >= 0 && val <= 1)
                minScore = val;
        }

        private void BtnCancelTemplate_Click(object sender, EventArgs e)
        {
            if (!templateCreated) return;
            templateCreated = false; // 取消模板匹配
            ResetView();
        }

        private void BtnBlobProcess_Click(object sender, EventArgs e)
        {
            if (flows.Count == 0) return;

            try
            {
                // 第一次点击时弹出设置窗
                if (!blobParamSet)
                {
                    using (var dlg = new BlobParamForm(blobMinGray, blobMaxGray, blobMinArea))
                    {
                        if (dlg.ShowDialog() == DialogResult.OK)
                        {
                            blobMinGray = dlg.MinGray;
                            blobMaxGray = dlg.MaxGray;
                            blobMinArea = dlg.MinArea;
                            blobParamSet = true;
                        }
                        else
                        {
                            return; // 用户取消
                        }
                    }
                }

                blobAnalysisEnabled = true; // 打开Blob分析
                RedisplayImage();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Blob分析出错: " + ex.Message);
            }
        }

        private void BtnCancelBlob_Click(object sender, EventArgs e)
        {
            blobAnalysisEnabled = false; // 关闭Blob分析
            RedisplayImage();
        }

        private void BtnPrev_Click(object sender, EventArgs e)
        {
            if (flows.Count == 0) return;

            currentIndex--;
            if (currentIndex < 0) currentIndex = flows.Count - 1;

            cbImageSelector.SelectedIndex = currentIndex;
            RedisplayImage();
        }

        private void BtnNext_Click(object sender, EventArgs e)
        {
            if (flows.Count == 0) return;

            currentIndex++;
            if (currentIndex >= flows.Count) currentIndex = 0;

            cbImageSelector.SelectedIndex = currentIndex;
            RedisplayImage();
        }

        private void BtnResetView_Click(object sender, EventArgs e)
        {
            ResetView();
        }
        #endregion

        #region 窗口大小自适应
        private void Form1_Resize(object sender, EventArgs e)
        {
            if (flows.Count == 0 || currentIndex < 0 || currentIndex >= flows.Count) return;
            FitImageToWindow();
            RedisplayImage();
        }

        private void FitImageToWindow()
        {
            try
            {
                if (imageWidth <= 0 || imageHeight <= 0) return;

                double winWidth = hWin.Width;
                double winHeight = hWin.Height;
                double winRatio = winWidth / winHeight;
                double imgRatio = (double)imageWidth / imageHeight;

                int newRow1, newCol1, newRow2, newCol2;

                if (winRatio > imgRatio)
                {
                    double dispHeight = imageHeight;
                    double dispWidth = imageHeight * winRatio;
                    double deltaW = (dispWidth - imageWidth) / 2;
                    newRow1 = 0;
                    newRow2 = imageHeight - 1;
                    newCol1 = (int)(-deltaW);
                    newCol2 = (int)(imageWidth - 1 + deltaW);
                }
                else
                {
                    double dispWidth = imageWidth;
                    double dispHeight = imageWidth / winRatio;
                    double deltaH = (dispHeight - imageHeight) / 2;
                    newCol1 = 0;
                    newCol2 = imageWidth - 1;
                    newRow1 = (int)(-deltaH);
                    newRow2 = (int)(imageHeight - 1 + deltaH);
                }

                hWin.HalconWindow.SetPart(newRow1, newCol1, newRow2, newCol2);
            }
            catch (Exception ex)
            {
                Console.WriteLine("窗口自适应错误: " + ex.Message);
            }
        }
        #endregion

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            foreach (var flow in flows)
                flow.Image?.Dispose();
            flows.Clear();

            halcon?.Dispose();
        }
    }
}
