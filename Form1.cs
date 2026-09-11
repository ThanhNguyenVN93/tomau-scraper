using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Drawing.Printing;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ToMauScraper
{
    public partial class Form1 : Form
    {
        private static readonly HttpClient _http = new HttpClient();
        private List<TrainhItem> _currentItems = new List<TrainhItem>();

        // ── Controls ──────────────────────────────────────────────────────────
        private TextBox txtSearch;
        private Button btnSearch;
        private Button btnRefresh;
        private Button btnDownloadAll;
        private ProgressBar progressBar;
        private Label lblStatus;
        private FlowLayoutPanel flowPanel;
        private Panel topPanel;

        public Form1()
        {
            InitializeComponent();
            SetupHttp();
            BuildUI();
            // Không set AcceptButton — tránh Form nuốt Enter trước IME
            this.KeyPreview = false;
        }

        // ─── HTTP Setup ───────────────────────────────────────────────────────
        private static void SetupHttp()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            _http.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36");
            _http.DefaultRequestHeaders.Add("Accept-Language", "vi-VN,vi;q=0.9");
            _http.Timeout = TimeSpan.FromSeconds(20);
        }

        private bool _placeholderActive = true;
        private const string _placeholder = "Nhập từ khóa... (vd: xe tải, hoa, mèo)";

        // ─── UI Builder ───────────────────────────────────────────────────────
        private void BuildUI()
        {
            this.Text = "ToMau.vn Scraper";
            this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            this.Size = new Size(1000, 720);
            this.MinimumSize = new Size(800, 500);
            this.BackColor = Color.FromArgb(245, 245, 250);
            this.Font = new Font("Segoe UI", 9f);

            // Top panel
            topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.FromArgb(52, 73, 94),
                Padding = new Padding(10, 10, 10, 10)
            };

            txtSearch = new TextBox
            {
                Location = new Point(12, 17),
                Width = 400,
                Height = 28,
                Font = new Font("Segoe UI", 10f),
                Text = _placeholder,
                ForeColor = Color.Gray,
                ImeMode = ImeMode.NoControl
            };

            // Placeholder dùng bool — không check ForeColor, tránh can thiệp IME tiếng Việt
            txtSearch.GotFocus += (s, e) =>
            {
                if (_placeholderActive)
                {
                    _placeholderActive = false;
                    txtSearch.Text = "";
                    txtSearch.ForeColor = Color.Black;
                }
            };
            txtSearch.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtSearch.Text))
                {
                    _placeholderActive = true;
                    txtSearch.Text = _placeholder;
                    txtSearch.ForeColor = Color.Gray;
                }
            };

            // Dùng KeyPress thay KeyDown — KeyDown suppress IME composition (Enter confirm)
            txtSearch.KeyPress += (s, e) =>
            {
                if (e.KeyChar == (char)Keys.Return)
                {
                    e.Handled = true;
                    BtnSearch_Click(null, null);
                }
            };

            btnSearch = new Button
            {
                Text = "🔍 Tìm kiếm",
                Location = new Point(420, 14),
                Size = new Size(110, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(41, 182, 246),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnSearch.FlatAppearance.BorderSize = 0;
            btnSearch.Click += BtnSearch_Click;

            btnRefresh = new Button
            {
                Text = "🔄 Làm mới",
                Location = new Point(540, 14),
                Size = new Size(100, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(230, 126, 34),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnRefresh.FlatAppearance.BorderSize = 0;
            btnRefresh.Click += BtnRefresh_Click;

            btnDownloadAll = new Button
            {
                Text = "⬇ Tải tất cả",
                Location = new Point(650, 14),
                Size = new Size(110, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(76, 175, 80),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Enabled = false
            };
            btnDownloadAll.FlatAppearance.BorderSize = 0;
            btnDownloadAll.Click += BtnDownloadAll_Click;

            topPanel.Controls.AddRange(new Control[] { txtSearch, btnSearch, btnRefresh, btnDownloadAll });
            this.Controls.Add(topPanel);




            // Status bar
            var statusPanel = new Panel { Dock = DockStyle.Bottom, Height = 28, BackColor = Color.FromArgb(236, 240, 241) };
            progressBar = new ProgressBar { Location = new Point(10, 5), Size = new Size(200, 18), Visible = false };
            lblStatus = new Label { Location = new Point(220, 6), AutoSize = true, ForeColor = Color.FromArgb(52, 73, 94) };
            statusPanel.Controls.AddRange(new Control[] { progressBar, lblStatus });
            this.Controls.Add(statusPanel);

            // Flow panel — Dock.Fill tự tính sau khi Top+Bottom đã dock
            flowPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Padding = new Padding(12, 14, 12, 20),
                BackColor = Color.FromArgb(222, 229, 240)
            };
            flowPanel.Resize += (s, e) => RecalculateScrollHeight();
            this.Controls.Add(flowPanel);
        }

        // ─── Search ───────────────────────────────────────────────────────────
        private async void BtnSearch_Click(object sender, EventArgs e) => await DoSearchAsync(forceRefresh: false);

        private async void BtnRefresh_Click(object sender, EventArgs e) => await DoSearchAsync(forceRefresh: true);

        private async Task DoSearchAsync(bool forceRefresh)
        {
            // Nếu placeholder đang active hoặc text rỗng thì bỏ qua
            if (_placeholderActive) return;
            string kw = txtSearch.Text.Trim();
            if (string.IsNullOrEmpty(kw)) return;

            SetStatus("Đang tìm kiếm...", true);
            btnSearch.Enabled = false;
            btnRefresh.Enabled = false;
            btnDownloadAll.Enabled = false;
            flowPanel.Controls.Clear();
            flowPanel.AutoScrollPosition = new Point(0, 0);
            _currentItems.Clear();

            try
            {
                int categoryCount;
                var cached = forceRefresh ? null : TryLoadCache(kw);
                if (cached != null)
                {
                    SetStatus($"(Cache) Đang nạp {cached.Items.Count} tranh...", true);
                    _currentItems.AddRange(cached.Items.Select(ci => new TrainhItem
                    {
                        Name = ci.Name,
                        Category = ci.Category,
                        PageUrl = ci.PageUrl,
                        ThumbnailUrl = ci.ThumbnailUrl
                    }));
                    categoryCount = cached.CategoryCount;
                }
                else
                {
                    // 1. Search page → get category URLs
                    string encoded = Uri.EscapeDataString(kw);
                    string searchUrl = $"https://tomau.vn/?s={encoded.Replace("%20", "+")}";
                    string searchHtml = await _http.GetStringAsync(searchUrl);

                    var categories = ParseSearchResults(searchHtml);

                    if (categories.Count == 0)
                    {
                        // Debug: show what we got
                        string snippet = searchHtml.Length > 500 ? searchHtml.Substring(0, 500) : searchHtml;
                        SetStatus($"Không tìm thấy kết quả nào cho \"{kw}\" (HTML={searchHtml.Length}b)", false);
                        // Uncomment line below to see raw HTML snippet in a popup for debugging:
                        // MessageBox.Show(snippet, "Debug HTML");
                        return;
                    }

                    SetStatus($"Tìm thấy {categories.Count} chủ đề. Đang tải danh sách tranh...", true);
                    progressBar.Maximum = categories.Count;
                    progressBar.Value = 0;
                    progressBar.Visible = true;

                    // 2. Tải song song các chủ đề (giới hạn 5 request cùng lúc) — nhanh hơn
                    // nhiều so với tải tuần tự từng chủ đề một, nhất là khi từ khóa phổ biến
                    // khớp hàng chục chủ đề.
                    using var throttle = new SemaphoreSlim(5);
                    int done = 0;
                    var fetchTasks = categories.Select(async cat =>
                    {
                        await throttle.WaitAsync();
                        try
                        {
                            string catHtml = await _http.GetStringAsync(cat.Url);
                            var items = ParseCategoryItems(catHtml, cat.Name);
                            this.Invoke((Action)(() =>
                            {
                                done++;
                                progressBar.Value = Math.Min(done, progressBar.Maximum);
                                SetStatus($"[{cat.Name}] → {items.Count} tranh ({done}/{categories.Count} chủ đề)", true);
                            }));
                            return items;
                        }
                        finally
                        {
                            throttle.Release();
                        }
                    });
                    var fetchResults = await Task.WhenAll(fetchTasks);
                    foreach (var items in fetchResults)
                        _currentItems.AddRange(items);

                    categoryCount = categories.Count;
                    SaveCache(kw, _currentItems, categoryCount);
                }

                // Cảnh báo nếu quá nhiều kết quả — hàng nghìn card cùng lúc có thể làm
                // FlowLayoutPanel nghẽn UI thread nặng (đã test thực tế: 2000+ kết quả
                // khiến app gần như treo vài phút).
                const int LargeResultWarningThreshold = 150;
                if (_currentItems.Count > LargeResultWarningThreshold)
                {
                    var confirm = MessageBox.Show(
                        $"Tìm thấy {_currentItems.Count} tranh — khá nhiều, có thể làm app chậm/đơ vài phút khi tải.\n\n" +
                        "Bấm \"Yes\" để tải toàn bộ, \"No\" để chỉ hiển thị " +
                        $"{LargeResultWarningThreshold} tranh đầu tiên.",
                        "Kết quả rất nhiều", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                    if (confirm == DialogResult.No)
                        _currentItems = _currentItems.Take(LargeResultWarningThreshold).ToList();
                }

                // 3. Render thumbnails
                SetStatus($"Đang tải {_currentItems.Count} thumbnail...", true);
                progressBar.Maximum = _currentItems.Count;
                progressBar.Value = 0;
                progressBar.Visible = true;

                await LoadThumbnailsAsync();
                flowPanel.AutoScrollPosition = new Point(0, 0);

                btnDownloadAll.Enabled = _currentItems.Count > 0;
                string cacheTag = cached != null ? " (cache)" : "";
                SetStatus($"✔ Hiển thị {_currentItems.Count} tranh từ {categoryCount} chủ đề{cacheTag}", false);
            }
            catch (Exception ex)
            {
                SetStatus(DescribeNetworkError(ex), false);
            }
            finally
            {
                btnSearch.Enabled = true;
                btnRefresh.Enabled = true;
                progressBar.Visible = false;
            }
        }

        // ─── Diễn giải lỗi mạng thành thông báo dễ hiểu ────────────────────────
        private static string DescribeNetworkError(Exception ex)
        {
            switch (ex)
            {
                case TaskCanceledException _:
                    return "⚠ Hết thời gian chờ — tomau.vn phản hồi quá lâu hoặc mất kết nối mạng.";
                case HttpRequestException _:
                    return "⚠ Không kết nối được tomau.vn — kiểm tra Internet hoặc thử lại sau.";
                default:
                    return $"Lỗi: {ex.Message}";
            }
        }

        // ─── Parse search results → list of category links ────────────────────
        private List<(string Name, string Url)> ParseSearchResults(string html)
        {
            var results = new List<(string, string)>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Structure on search page:
            // <li>
            //   <a href="URL"><img ...></a>
            //   <a href="URL">Name</a>
            // </li>
            // Extract all tomau.vn internal links with text content
            var matches = Regex.Matches(html,
                @"href=""(https://tomau\.vn/to-mau-[^""#?]+?)""[^>]*>\s*([^<]{3,}?)\s*</a>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            foreach (Match m in matches)
            {
                string url = m.Groups[1].Value.Trim().TrimEnd('/') + "/";
                string name = HtmlDecode(m.Groups[2].Value.Trim());
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (name.Length > 80) continue; // skip garbage
                if (!seen.Add(url)) continue;
                results.Add((name, url));
            }
            return results;
        }

        // ─── Parse category page → list of TrainhItem ─────────────────────────
        private List<TrainhItem> ParseCategoryItems(string html, string category)
        {
            var items = new List<TrainhItem>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Each item block (simplified — match img src + surrounding anchor href):
            // <a href="PAGE_URL"><img src="THUMB_URL" title="NAME" ...></a>
            var matches = Regex.Matches(html,
                @"<a\s[^>]*href=""(https://tomau\.vn/to-mau-[^/""]+/[^""]+)""[^>]*>\s*<img\s[^>]*src=""([^""]+)""[^>]*title=""([^""]+)""",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            foreach (Match m in matches)
            {
                string pageUrl = m.Groups[1].Value.Trim();
                string imgUrl  = m.Groups[2].Value.Trim();
                string name    = HtmlDecode(m.Groups[3].Value.Trim());

                if (!imgUrl.Contains("wp-content/uploads")) continue;
                if (imgUrl.Contains("logo")) continue;
                if (!seen.Add(imgUrl)) continue;

                items.Add(new TrainhItem
                {
                    Name         = name,
                    PageUrl      = pageUrl,
                    ThumbnailUrl = imgUrl,
                    Category     = category
                });
            }

            // fallback: img without title attribute
            if (items.Count == 0)
            {
                var fb = Regex.Matches(html,
                    @"<a\s[^>]*href=""(https://tomau\.vn/to-mau-[^/""]+/[^""]+)""[^>]*>\s*<img\s[^>]*src=""([^""]+)""",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);

                foreach (Match m in fb)
                {
                    string pageUrl = m.Groups[1].Value.Trim();
                    string imgUrl  = m.Groups[2].Value.Trim();
                    if (!imgUrl.Contains("wp-content/uploads")) continue;
                    if (imgUrl.Contains("logo")) continue;
                    if (!seen.Add(imgUrl)) continue;

                    // derive name from URL
                    string slug = Uri.UnescapeDataString(pageUrl.TrimEnd('/').Split('/').Last())
                                     .Replace("-", " ");
                    items.Add(new TrainhItem
                    {
                        Name         = slug,
                        PageUrl      = pageUrl,
                        ThumbnailUrl = imgUrl,
                        Category     = category
                    });
                }
            }

            return items;
        }

        // ─── Load thumbnails async ─────────────────────────────────────────────
        private async Task LoadThumbnailsAsync()
        {
            int loaded = 0;
            foreach (var item in _currentItems)
            {
                var card = CreateCard(item);
                flowPanel.Controls.Add(card);

                // Load thumbnail in background
                var capturedItem = item;
                var capturedCard = card;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        Bitmap bmp = LoadThumbFromDiskCache(capturedItem.ThumbnailUrl);
                        if (bmp == null)
                        {
                            var bytes = await _http.GetByteArrayAsync(capturedItem.ThumbnailUrl);
                            using var ms = new MemoryStream(bytes);
                            using var original = Image.FromStream(ms);
                            bmp = CompressForCache(original);
                            SaveThumbToDiskCache(bmp, capturedItem.ThumbnailUrl);
                        }
                        capturedItem.Thumbnail = bmp;

                        this.Invoke((Action)(() =>
                        {
                            var pb = capturedCard.Controls.OfType<PictureBox>().FirstOrDefault();
                            if (pb != null) { pb.Image = bmp; pb.Invalidate(); }
                            loaded++;
                            progressBar.Value = Math.Min(loaded, progressBar.Maximum);
                        }));
                    }
                    catch { /* skip failed thumbnails */ }
                });

                await Task.Delay(30); // throttle
            }

            RecalculateScrollHeight();
        }

        // ─── Tính chiều cao cuộn thủ công ───────────────────────────────────────
        // FlowLayoutPanel.AutoScroll tự tính chiều cao cuộn không đáng tin cậy khi
        // Padding/Margin không đồng nhất (đã gặp bug bỏ qua Padding.Top, và bug cắt
        // mất hàng cuối) — nên tự tính rows × row-height và set AutoScrollMinSize
        // trực tiếp, không phụ thuộc cơ chế tự động của control.
        private const int CardOuterWidth = 170 + 8 + 8;   // Size.Width + Margin trái/phải
        private const int CardOuterHeight = 210 + 60 + 8; // Size.Height + Margin trên/dưới

        private void RecalculateScrollHeight()
        {
            if (_currentItems.Count == 0) return;

            int usableWidth = Math.Max(CardOuterWidth, flowPanel.ClientSize.Width - flowPanel.Padding.Horizontal);
            int columns = Math.Max(1, usableWidth / CardOuterWidth);
            int rows = (int)Math.Ceiling(_currentItems.Count / (double)columns);
            int neededHeight = rows * CardOuterHeight + flowPanel.Padding.Vertical + 20; // đệm an toàn

            flowPanel.AutoScrollMinSize = new Size(0, neededHeight);
        }

        // ─── Card builder ─────────────────────────────────────────────────────
        private Panel CreateCard(TrainhItem item)
        {
            var card = new Panel
            {
                Size = new Size(170, 210),
                BackColor = Color.White,
                Cursor = Cursors.Hand,
                Margin = new Padding(8, 60, 8, 8),
                Tag = item
            };
            card.Paint += Card_Paint;

            var pb = new PictureBox
            {
                Location = new Point(5, 5),
                Size = new Size(160, 145),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(245, 245, 245)
            };
            card.Controls.Add(pb);

            var lblName = new Label
            {
                Text = item.Name,
                Location = new Point(5, 155),
                Size = new Size(160, 34),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = Color.FromArgb(44, 62, 80)
            };
            card.Controls.Add(lblName);

            var btnDl = new Button
            {
                Text = "⬇ Tải về",
                Location = new Point(5, 186),
                Size = new Size(78, 22),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(41, 182, 246),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Tag = item
            };
            btnDl.FlatAppearance.BorderSize = 0;
            btnDl.Click += async (s, e) => await DownloadSingleAsync(item);
            card.Controls.Add(btnDl);

            var btnPrint = new Button
            {
                Text = "🖨 In",
                Location = new Point(87, 186),
                Size = new Size(78, 22),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(155, 89, 182),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Tag = item
            };
            btnPrint.FlatAppearance.BorderSize = 0;
            btnPrint.Click += async (s, e) => await PrintItemAsync(item);
            card.Controls.Add(btnPrint);

            // Click card = open in browser
            pb.Click += (s, e) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(item.PageUrl) { UseShellExecute = true });

            return card;
        }

        private void Card_Paint(object sender, PaintEventArgs e)
        {
            var p = sender as Panel;
            ControlPaint.DrawBorder(e.Graphics, p.ClientRectangle,
                Color.FromArgb(220, 220, 230), 1, ButtonBorderStyle.Solid,
                Color.FromArgb(220, 220, 230), 1, ButtonBorderStyle.Solid,
                Color.FromArgb(220, 220, 230), 1, ButtonBorderStyle.Solid,
                Color.FromArgb(220, 220, 230), 1, ButtonBorderStyle.Solid);
        }

        // ─── Download single ──────────────────────────────────────────────────
        private async Task DownloadSingleAsync(TrainhItem item)
        {
            using var dlg = new FolderBrowserDialog { Description = "Chọn thư mục lưu ảnh" };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            await DownloadItemAsync(item, dlg.SelectedPath);
            SetStatus($"✔ Đã tải: {item.Name}", false);
        }

        // ─── In trực tiếp ──────────────────────────────────────────────────────
        // Dùng PrintDocument + PrintPreviewDialog của WinForms: xem trước, đổi
        // orientation/margin qua Page Setup, rồi in — không phụ thuộc app ảnh mặc định.
        private async Task PrintItemAsync(TrainhItem item)
        {
            SetStatus($"Đang chuẩn bị in: {item.Name}...", true);
            try
            {
                string imgUrl = await GetFullResImageUrl(item) ?? item.ThumbnailUrl;
                var bytes = await DownloadImageBytesAsync(imgUrl, item.PageUrl)
                            ?? await DownloadImageBytesAsync(item.ThumbnailUrl, item.PageUrl);
                if (bytes == null)
                {
                    MessageBox.Show($"Không tải được ảnh để in: {item.Name}", "Lỗi",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                using var ms = new MemoryStream(bytes);
                using var bmp = new Bitmap(ms);

                using var printDoc = new PrintDocument();
                printDoc.DocumentName = item.Name;
                printDoc.PrintPage += (s, e) =>
                {
                    var bounds = e.MarginBounds;
                    float scale = Math.Min((float)bounds.Width / bmp.Width, (float)bounds.Height / bmp.Height);
                    int w = (int)(bmp.Width * scale);
                    int h = (int)(bmp.Height * scale);
                    int x = bounds.X + (bounds.Width - w) / 2;
                    int y = bounds.Y + (bounds.Height - h) / 2;
                    e.Graphics.DrawImage(bmp, x, y, w, h);
                };

                using var previewDlg = new PrintPreviewDialog
                {
                    Document = printDoc,
                    Width = 800,
                    Height = 700,
                    StartPosition = FormStartPosition.CenterScreen
                };
                previewDlg.ShowDialog();
                SetStatus($"Đóng xem trước: {item.Name}", false);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi in: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ─── Download All ─────────────────────────────────────────────────────
        private async void BtnDownloadAll_Click(object sender, EventArgs e)
        {
            using var dlg = new FolderBrowserDialog { Description = "Chọn thư mục lưu tất cả ảnh" };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            btnDownloadAll.Enabled = false;
            btnSearch.Enabled = false;
            progressBar.Maximum = _currentItems.Count;
            progressBar.Value = 0;
            progressBar.Visible = true;

            int ok = 0, fail = 0;
            foreach (var item in _currentItems)
            {
                try
                {
                    await DownloadItemAsync(item, dlg.SelectedPath);
                    ok++;
                }
                catch { fail++; }
                progressBar.Value = ok + fail;
                SetStatus($"Đang tải... {ok}/{_currentItems.Count}", false);
            }

            progressBar.Visible = false;
            btnDownloadAll.Enabled = true;
            btnSearch.Enabled = true;
            SetStatus($"✔ Hoàn thành: {ok} ảnh, {fail} lỗi", false);
            MessageBox.Show($"Tải xong!\n✔ Thành công: {ok}\n✖ Lỗi: {fail}", "Kết quả",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ─── Core download logic ──────────────────────────────────────────────
        private async Task DownloadItemAsync(TrainhItem item, string folder)
        {
            // Try to get the full-res image from the item page
            string imgUrl = await GetFullResImageUrl(item) ?? item.ThumbnailUrl;
            var bytes = await DownloadImageBytesAsync(imgUrl, item.PageUrl);
            if (bytes == null)
            {
                // Ảnh full-res bị chặn hotlink / lỗi → thử lại với thumbnail
                bytes = await DownloadImageBytesAsync(item.ThumbnailUrl, item.PageUrl);
                imgUrl = item.ThumbnailUrl;
            }
            if (bytes == null)
                throw new InvalidDataException($"Không tải được ảnh hợp lệ cho \"{item.Name}\"");

            string ext = Path.GetExtension(new Uri(imgUrl).AbsolutePath);
            if (string.IsNullOrEmpty(ext)) ext = ".jpg";

            string safeName = Regex.Replace(item.Name, @"[\\/:*?""<>|]", "_");
            string path = Path.Combine(folder, safeName + ext);

            // avoid overwrite
            int i = 1;
            while (File.Exists(path))
                path = Path.Combine(folder, $"{safeName}_{i++}{ext}");

            await Task.Run(() => File.WriteAllBytes(path, bytes));
        }

        // ─── Tải ảnh kèm Referer (tránh bị chặn hotlink), kiểm tra magic bytes ──
        private static async Task<byte[]> DownloadImageBytesAsync(string url, string refererUrl)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Referrer = new Uri(refererUrl);
                using var resp = await _http.SendAsync(req);
                if (!resp.IsSuccessStatusCode) return null;
                var bytes = await resp.Content.ReadAsByteArrayAsync();
                return IsValidImage(bytes) ? bytes : null;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsValidImage(byte[] b)
        {
            if (b == null || b.Length < 12) return false;
            if (b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return true; // JPEG
            if (b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return true; // PNG
            if (b[0] == 0x47 && b[1] == 0x49 && b[2] == 0x46) return true; // GIF
            if (b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46 &&
                b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50) return true; // WEBP
            return false;
        }

        // ─── Get full-res URL from item page ──────────────────────────────────
        private async Task<string> GetFullResImageUrl(TrainhItem item)
        {
            try
            {
                string html = await _http.GetStringAsync(item.PageUrl);
                // Look for the main content image (og:image or biggest img in content)
                var ogMatch = Regex.Match(html, @"<meta\s+property=""og:image""\s+content=""([^""]+)""", RegexOptions.IgnoreCase);
                if (ogMatch.Success) return ogMatch.Groups[1].Value;

                // fallback: look for full-size image link
                var imgMatch = Regex.Match(html,
                    @"https://tomau\.vn/wp-content/uploads/[^""'\s]+\.(png|jpg|jpeg|webp)",
                    RegexOptions.IgnoreCase);
                if (imgMatch.Success) return imgMatch.Value;
            }
            catch { }
            return null;
        }

        // ─── Cache thumbnail ra đĩa (JPEG nén nặng, resize nhỏ để tiết kiệm dung
        // lượng) — %LocalAppData%\ToMauScraper\thumbcache, key theo MD5 của URL ──
        private static string ThumbCacheDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ToMauScraper", "thumbcache");

        private static string ThumbCachePath(string url)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(url));
            string hex = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            return Path.Combine(ThumbCacheDir, hex + ".jpg");
        }

        private static Bitmap LoadThumbFromDiskCache(string url)
        {
            try
            {
                string path = ThumbCachePath(url);
                if (!File.Exists(path)) return null;
                using var fs = File.OpenRead(path);
                using var img = Image.FromStream(fs);
                return new Bitmap(img); // clone để tách khỏi stream trước khi đóng
            }
            catch
            {
                return null;
            }
        }

        private static void SaveThumbToDiskCache(Bitmap bmp, string url)
        {
            try
            {
                Directory.CreateDirectory(ThumbCacheDir);
                var jpegCodec = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders()
                    .First(c => c.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid);
                var encParams = new System.Drawing.Imaging.EncoderParameters(1);
                encParams.Param[0] = new System.Drawing.Imaging.EncoderParameter(
                    System.Drawing.Imaging.Encoder.Quality, 35L); // siêu nén
                bmp.Save(ThumbCachePath(url), jpegCodec, encParams);
            }
            catch { /* cache lỗi thì bỏ qua */ }
        }

        // Resize nhỏ (tối đa 260px cạnh dài) trước khi nén — vừa đủ hiển thị thumbnail
        private static Bitmap CompressForCache(Image original, int maxDim = 260)
        {
            double scale = Math.Min(1.0, (double)maxDim / Math.Max(original.Width, original.Height));
            int w = Math.Max(1, (int)(original.Width * scale));
            int h = Math.Max(1, (int)(original.Height * scale));

            var resized = new Bitmap(w, h);
            using (var g = Graphics.FromImage(resized))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(original, 0, 0, w, h);
            }
            return resized;
        }

        // ─── Cache tìm kiếm ra file (JSON, %LocalAppData%\ToMauScraper\cache) ──
        private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

        private static string CacheDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ToMauScraper", "cache");

        private static string CacheFilePath(string keyword)
        {
            string safeKey = Regex.Replace(keyword.Trim().ToLowerInvariant(), @"[^a-z0-9À-ỹ]+", "_");
            return Path.Combine(CacheDir, safeKey + ".json");
        }

        private static CachedResult TryLoadCache(string keyword)
        {
            try
            {
                string path = CacheFilePath(keyword);
                if (!File.Exists(path)) return null;
                if (DateTime.UtcNow - File.GetLastWriteTimeUtc(path) > CacheTtl) return null;

                using var fs = File.OpenRead(path);
                var serializer = new DataContractJsonSerializer(typeof(CachedResult));
                return (CachedResult)serializer.ReadObject(fs);
            }
            catch
            {
                return null;
            }
        }

        private static void SaveCache(string keyword, List<TrainhItem> items, int categoryCount)
        {
            try
            {
                Directory.CreateDirectory(CacheDir);
                var data = new CachedResult
                {
                    CategoryCount = categoryCount,
                    Items = items.Select(i => new CachedItem
                    {
                        Name = i.Name,
                        Category = i.Category,
                        PageUrl = i.PageUrl,
                        ThumbnailUrl = i.ThumbnailUrl
                    }).ToList()
                };

                using var fs = File.Create(CacheFilePath(keyword));
                var serializer = new DataContractJsonSerializer(typeof(CachedResult));
                serializer.WriteObject(fs, data);
            }
            catch { /* cache lỗi thì bỏ qua, không ảnh hưởng tìm kiếm */ }
        }

        // ─── Helpers ──────────────────────────────────────────────────────────
        private void SetStatus(string msg, bool busy)
        {
            lblStatus.Text = msg;
            progressBar.Visible = busy;
            this.UseWaitCursor = busy;
        }

        private static string HtmlDecode(string s)
        {
            return WebUtility.HtmlDecode(s);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _http?.Dispose();
                if (components != null) components.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // ─── Data model ───────────────────────────────────────────────────────────
    public class TrainhItem
    {
        public string Name { get; set; }
        public string Category { get; set; }
        public string PageUrl { get; set; }
        public string ThumbnailUrl { get; set; }
        public Bitmap Thumbnail { get; set; }
    }

    // ─── DTO cho cache file (JSON) — không chứa Bitmap ─────────────────────────
    [DataContract]
    public class CachedItem
    {
        [DataMember] public string Name { get; set; }
        [DataMember] public string Category { get; set; }
        [DataMember] public string PageUrl { get; set; }
        [DataMember] public string ThumbnailUrl { get; set; }
    }

    [DataContract]
    public class CachedResult
    {
        [DataMember] public int CategoryCount { get; set; }
        [DataMember] public List<CachedItem> Items { get; set; }
    }
}
