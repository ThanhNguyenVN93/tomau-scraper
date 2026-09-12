# ToMau.vn Scraper

Tool C# WinForms để tìm kiếm, xem trước, in và tải tranh tô màu từ [tomau.vn](https://tomau.vn).

<img src="icon.png" width="64" height="64" alt="Icon ứng dụng" />

## Yêu cầu

- Windows 7 SP1 trở lên
- .NET Framework 4.7.2 trở lên ([tải tại đây](https://dotnet.microsoft.com/download/dotnet-framework/net472) nếu chưa có)
- Để build từ source: .NET SDK (dotnet CLI) hoặc Visual Studio 2019+

## Cách build & chạy

```bash
dotnet build ToMauScraper.csproj
dotnet run --project ToMauScraper.csproj
```

Hoặc mở `ToMauScraper.csproj` bằng Visual Studio và nhấn F5 / Ctrl+F5.

File thực thi sau khi build nằm tại `bin/Debug/net472/ToMauScraper.exe`.

## Tính năng

- 🔍 Nhập từ khóa → tìm tất cả chủ đề liên quan trên tomau.vn, tải song song (giới hạn 5 request cùng lúc) cho tốc độ tốt hơn
- 🖼️ Hiển thị grid thumbnail của từng tranh
- 🌐 Click vào ảnh → mở trang gốc trong trình duyệt
- ⬇ Nút "Tải về" trên từng ảnh → chọn thư mục, tải file gốc full-res (tự động kiểm tra định dạng ảnh hợp lệ, fallback sang thumbnail nếu bị chặn hotlink)
- ⬇ Nút "Tải tất cả" → tải hàng loạt toàn bộ kết quả tìm kiếm
- 🖨 Nút "In" trên từng ảnh → xem trước và in trực tiếp qua `PrintPreviewDialog`
- 🔄 Nút "Làm mới" → bỏ qua cache, tìm lại trực tiếp từ tomau.vn
- 💾 Cache kết quả tìm kiếm ra file (24h) và cache thumbnail nén trên đĩa — tìm lại từ khóa cũ gần như tức thì
- ⌨️ Hỗ trợ gõ tiếng Việt qua bộ gõ ngoài (Unikey, EVKey...) trong ô tìm kiếm

## Cấu trúc code

| File | Mô tả |
|------|-------|
| `Form1.cs` | Logic chính: search, scrape, UI, download, in, cache |
| `Form1.Designer.cs` | InitializeComponent stub |
| `Program.cs` | Entry point — kiểm tra OS/.NET runtime trước khi chạy |
| `app.manifest` | Application manifest — khai báo hỗ trợ Windows 7–11, DPI-aware |
| `app.ico` | Icon ứng dụng |

## Lưu ý kỹ thuật

- Tool đọc full-res image từ thẻ `og:image` của từng trang tranh, có kiểm tra magic bytes để tránh lưu nhầm trang lỗi (HTML) thành ảnh
- Gửi kèm header `Referer` khi tải ảnh full-res để tránh bị chặn hotlink
- Cache tìm kiếm lưu tại `%LocalAppData%\ToMauScraper\cache`, cache thumbnail (nén JPEG) tại `%LocalAppData%\ToMauScraper\thumbcache`
- Hỗ trợ tải các định dạng .png, .jpg, .jpeg, .webp
- Nếu máy chưa cài đủ .NET Framework 4.7.2, app sẽ báo và hướng dẫn link tải, không tự động cài

## Giấy phép

Phát hành theo giấy phép [MIT](LICENSE).

## Miễn trừ trách nhiệm

Tool này chỉ dùng cho mục đích cá nhân, học tập. Vui lòng tôn trọng điều khoản sử dụng của tomau.vn và không dùng để spam/tải quá mức gây ảnh hưởng đến server của họ.
