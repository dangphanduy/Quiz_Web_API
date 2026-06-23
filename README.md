# 📚 Quiz & Flashcards Learning Platform

<!-- Badges -->
[![.NET Core](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&style=flat-square)](https://dotnet.microsoft.com/)
[![SQL Server](https://img.shields.io/badge/SQL%20Server-2022-CC2927?logo=microsoft-sql-server&style=flat-square)](https://www.microsoft.com/sql-server)
[![Docker](https://img.shields.io/badge/Docker-Supported-2496ED?logo=docker&style=flat-square)](https://www.docker.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](LICENSE)

Dự án **Quiz & Flashcards Learning Platform** là một nền tảng học tập trực tuyến hiện đại được xây dựng dựa trên công nghệ **ASP.NET Core 9.0 (MVC & Web API)** và **SQL Server**. Hệ thống cung cấp giải pháp học tập toàn diện từ việc xem bài học video/văn bản, thực hành trắc nghiệm, ghi nhớ qua Flashcard, trao đổi trực tuyến cho đến thanh toán gói hội viên và cấp chứng chỉ tự động tích hợp công nghệ AI.

---

## 👥 Thành viên nhóm thực hiện

Dưới đây là danh sách các thành viên tham gia nghiên cứu và phát triển dự án:

| STT | Họ và Tên | MSSV |
| :--- | :--- | :--- |
| 1 | **Đặng Phan Duy** | 6451071010 |
| 2 | **Đào Tiến Đạt** | 6451071017 |
| 3 | **Lê Trần Minh Khôi** | 6451071036 |
| 4 | **Nguyễn Đức Vinh** | 6451071087 |

---

## 🛠️ Công nghệ sử dụng (Tech Stack)

- **Backend**: ASP.NET Core 9.0 (Web API + MVC), EF Core, LINQ
- **Database**: SQL Server 2022
- **Real-time Communication**: SignalR (Hỗ trợ Chat thời gian thực)
- **AI Integration**: Google Gemini API (Trợ lý ảo hỗ trợ học tập)
- **Payment Gateway**: Cổng thanh toán PayOS (Thanh toán giỏ hàng, gia hạn hội viên)
- **Cloud Storage**: Google Cloud Storage (Lưu trữ video bài giảng, hình ảnh, tài liệu)
- **Certificate Drawing**: SkiaSharp (Vẽ chứng chỉ tự động trên môi trường Docker/Linux)
- **Logging**: Serilog (Phân tách log chi tiết theo từng module)

---

## 🚀 Mô tả chức năng hệ thống

Hệ thống được thiết kế theo mô hình phân quyền chặt chẽ với 3 vai trò chính: **Học viên (Student)**, **Giảng viên (Instructor)** và **Quản trị viên (Admin)**.

### 1. Phân hệ dành cho Học viên (Student)
- **Đăng ký & Đăng nhập đa phương thức**: Hỗ trợ đăng nhập thông thường (mã hóa mật khẩu), Cookie & JWT token, hoặc đăng nhập mạng xã hội (Google, Facebook). Tính năng **Quên mật khẩu** gửi mã xác minh OTP qua Email.
- **Duyệt & Tìm kiếm khóa học**: Lọc khóa học theo danh mục (Category), tìm kiếm thông minh theo từ khóa.
- **Học tập trực tuyến trực quan**:
  - Xem bài giảng dưới dạng video hoặc nội dung bài viết.
  - Theo dõi tiến trình học tập (Course Progress) chi tiết theo từng bài học.
- **Thẻ ghi nhớ (Flashcards)**:
  - Học từ vựng, định nghĩa hoặc công thức qua thẻ ghi nhớ với hiệu ứng lật thẻ mượt mà.
  - Tự tạo các bộ Flashcard cá nhân hoặc học từ các bộ Flashcard công khai (Public).
- **Làm bài kiểm tra (Tests & Quizzes)**:
  - Làm bài trắc nghiệm tính giờ thực tế để đánh giá năng lực.
  - Xem kết quả trực tiếp, bảng điểm và lịch sử làm bài kiểm tra.
- **Thanh toán & Gói hội viên**:
  - Quản lý giỏ hàng trực tuyến (Cart).
  - Thanh toán linh hoạt qua cổng PayOS (QR Code chuyển khoản nhanh).
  - Đăng ký gói thuê bao hội viên (Subscription) để mở khóa tất cả các khóa học trên hệ thống.
- **Hệ thống Chat & Hỗ trợ AI**:
  - Giao tiếp thời gian thực (Chat SignalR) với giảng viên hoặc các học viên khác.
  - Trợ lý chatbot thông minh tích hợp **Gemini AI** giải đáp kiến thức bài học tức thì.
- **Cấp chứng chỉ tự động (Certificates)**:
  - Nhận chứng chỉ hoàn thành khóa học dạng file ảnh/PDF được vẽ tự động bằng SkiaSharp và gửi trực tiếp qua Email khi hoàn thành 100% khóa học.

### 2. Phân hệ dành cho Giảng viên (Instructor)
- **Quản lý khóa học giảng dạy**: Thêm mới, chỉnh sửa nội dung bài giảng, chương trình học (Chapters & Lessons).
- **Tạo ngân hàng câu hỏi & Bài kiểm tra**: Thiết lập các bài kiểm tra trắc nghiệm tương ứng với bài học.
- **Quản lý Flashcards**: Tạo và gán bộ Flashcards bổ trợ cho bài học.

### 3. Phân hệ dành cho Quản trị viên (Admin)
- **Dashboard Thống kê trực quan**: Xem doanh thu bán khóa học/gói hội viên, số lượng người dùng mới, và các khóa học được đăng ký nhiều nhất.
- **Quản lý danh mục & Khóa học**: Phê duyệt hoặc khóa các khóa học vi phạm điều khoản.
- **Quản trị người dùng & Phân quyền**: Cấp quyền Admin, Instructor hoặc Student cho tài khoản trong hệ thống.
- **Hệ thống Logging (Serilog)**: Phân tách log tự động thành các thư mục riêng biệt giúp dễ dàng quản trị và giám sát:
  - `requests/`: Log chi tiết các truy cập HTTP.
  - `payment/`: Log giao dịch, thanh toán qua PayOS.
  - `chat/`: Log lịch sử và tin nhắn chat.
  - `learning/`: Log tiến trình học tập, bài thi, flashcard.
  - `user/`: Log đăng ký, đăng nhập, bảo mật.
  - `errors/`: Log lỗi hệ thống cấp độ Error/Fatal.

---

## 💻 Hướng dẫn chạy Project

### Cách 1: Chạy trực tiếp trên máy local (Dotnet CLI)

#### Yêu cầu chuẩn bị
- Cài đặt [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).
- Cài đặt [SQL Server](https://www.microsoft.com/sql-server) (bản LocalDB hoặc Express).

#### Các bước thực hiện
1. **Clone mã nguồn**:
   ```bash
   git clone https://github.com/dangphanduy/Quiz_Web_API.git
   cd Quiz_Web_API
   ```
2. **Cấu hình Cơ sở dữ liệu**:
   - Mở file `Quiz_Web/appsettings.Development.json`.
   - Cập nhật chuỗi kết nối Database tại `DefaultConnection` phù hợp với cấu hình SQL Server trên máy của bạn.
   - Sử dụng file script `Quiz_Web/LearningPlatform.sql` chạy trên SQL Server Management Studio (SSMS) để tạo database và cấu trúc bảng.
   - *(Tùy chọn)* Chạy thêm các file script trong thư mục `Database/` để nạp dữ liệu danh mục mẫu (`SeedCategoriesNavigation.sql`) và dữ liệu bài giảng mẫu (`SeedData_RealVideos_Updated.sql`).
3. **Cấu hình API Key (PayOS & Gemini AI)**:
   - Cập nhật thông tin Client ID, API Key, Checksum Key trong phần `PayOSSettings`.
   - Điền API Key của Google Gemini vào phần `Gemini` để sử dụng tính năng Chatbot AI hỗ trợ học tập.
4. **Phục hồi và chạy dự án**:
   ```bash
   cd Quiz_Web
   dotnet restore
   dotnet build
   dotnet run
   ```
5. Truy cập ứng dụng tại địa chỉ: `https://localhost:7158` (hoặc cổng HTTP hiển thị trên console).

---

### Cách 2: Chạy thông qua Docker Compose (Khuyên dùng)

Hệ thống đã được thiết lập sẵn môi trường Container hóa giúp triển khai nhanh chóng.

1. Đảm bảo máy tính của bạn đã cài đặt và đang chạy **Docker Desktop**.
2. Tại thư mục gốc dự án (chứa file `docker-compose.yml`), khởi tạo một file `.env` hoặc chỉnh sửa trực tiếp mật khẩu SQL Server tại biến `${DB_PASSWORD}`.
3. Chạy lệnh deploy:
   ```bash
   docker-compose up -d --build
   ```
4. Docker sẽ tự động pull image SQL Server, build ứng dụng ASP.NET Core 9.0 (cài đặt sẵn fontconfig và các thư viện hỗ trợ xuất chứng chỉ trên Linux) và kết nối chúng lại với nhau.
5. Truy cập ứng dụng tại: `http://localhost:8080`

---

## 🔗 Link Swagger UI

Khi chạy ứng dụng ở môi trường **Development**, tài liệu hướng dẫn và thử nghiệm API tích hợp sẵn qua Swagger UI có thể truy cập tại:

- **Địa chỉ chạy Local**: [https://localhost:7158/swagger](https://localhost:7158/swagger)
- **Địa chỉ chạy Docker**: [http://localhost:8080/swagger](http://localhost:8080/swagger)

*(Lưu ý: Swagger UI chỉ hiển thị các API bắt đầu bằng tiền tố `/api/...` để phục vụ phát triển ứng dụng di động hoặc bên thứ ba).*
