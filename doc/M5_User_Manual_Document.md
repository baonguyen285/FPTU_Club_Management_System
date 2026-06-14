# Hướng dẫn sử dụng hệ thống FPTU Club Management

Tài liệu này hướng dẫn cách khởi chạy và chạy thử toàn bộ luồng hệ thống Microservices thông qua API Gateway bằng Docker.

---

## 1. Yêu cầu hệ thống
- Máy tính phải được cài đặt **Docker Desktop** và đang ở trạng thái hoạt động (Running).
- Đã cài đặt **Postman** để gọi API.
- File HTML dùng để test (đã có sẵn trong dự án: `test-signalr-gateway.html`).

---

## 2. Cách khởi động hệ thống (Chỉ cần 1 chạm)

Thay vì phải chạy từng phần mềm (SQL Server, Redis, 4 con API khác nhau), Docker sẽ gom mọi thứ chạy trong nền chỉ với 1 câu lệnh duy nhất.

1. Mở Terminal (hoặc Command Prompt) tại thư mục gốc của dự án.
2. Gõ câu lệnh sau:
   ```bash
   docker-compose up -d
   ```
3. Đợi khoảng 10 - 20 giây để 7 thành phần của hệ thống cùng khởi động.
4. (Tuỳ chọn) Nếu bạn muốn dừng hệ thống và **xóa sạch dữ liệu cũ** (Rất hữu ích khi code có thay đổi cấu trúc bảng Database), hãy dùng lệnh:
   ```bash
   docker-compose down -v
   ```

---

## 3. Kiến trúc Cổng giao tiếp (Port)

Toàn bộ hệ thống đều bị chặn lại và chỉ có một Cửa ngõ duy nhất cho phép truy cập từ bên ngoài, đó là **API Gateway (Cổng 5000)**.
- Mọi API (Auth, Report, Club) đều được đổi URL qua cổng 5000: `http://localhost:5000/gateway/...`
- SignalR WebSocket cũng đi qua Cổng 5000: `ws://localhost:5000/gateway/hubs/notification`

---

## 4. Hướng dẫn Test luồng chính từ A đến Z

### Bước 4.1: Đăng ký tài khoản Admin (Postman)
Vì hệ thống mới chạy lên Database sẽ trống rỗng, bạn cần tạo một tài khoản đầu tiên.
- **Method:** `POST`
- **URL:** `http://localhost:5000/gateway/auth/register`
- **Body (raw JSON):**
  ```json
  {
    "email": "admin@fptu.edu.vn",
    "password": "Password123!",
    "fullName": "Ban Quan Ly",
    "role": "Admin"
  }
  ```
- **Kết quả:** Nhận về Status 201 Created.

### Bước 4.2: Đăng nhập và lấy Token (Postman)
- **Method:** `POST`
- **URL:** `http://localhost:5000/gateway/auth/login`
- **Body (raw JSON):**
  ```json
  {
    "email": "admin@fptu.edu.vn",
    "password": "Password123!"
  }
  ```
- **Kết quả:** Trong `data.token` của kết quả trả về sẽ có một chuỗi mã hóa cực dài. Hãy Copy chuỗi Token đó.

### Bước 4.3: Mở trạm Thu sóng (SignalR Web)
- Mở file `test-signalr-gateway.html` trong máy tính bằng trình duyệt web (Chrome/Edge).
- Dán Token vừa copy ở Bước 4.2 vào ô nhập.
- Bấm **"Kết nối ngay!"**.
- Nếu thấy dòng chữ `🟢 KẾT NỐI QUA GATEWAY THÀNH CÔNG`, hãy để nguyên màn hình đó (Đây là màn hình nhận thông báo).

### Bước 4.4: Nộp một Báo cáo mới (Postman)
Chúng ta sẽ giả lập hành động của một Chủ nhiệm CLB đi nộp báo cáo, để xem Admin ở Bước 4.3 có nhận được thông báo không.
- **Method:** `POST`
- **URL:** `http://localhost:5000/gateway/reports`
- **Headers:** Thêm cấu hình `Authorization` chọn loại `Bearer Token` và dán Token ở Bước 4.2 vào.
- **Body (raw JSON):**
  ```json
  {
    "clubId": "99999999-9999-9999-9999-999999999999",
    "title": "Báo cáo Tháng 6",
    "content": "Hoạt động CLB tháng 6 diễn ra thành công tốt đẹp.",
    "type": 1
  }
  ```
- Bấm **Send**.

### Bước 4.5: Xem điều kỳ diệu (Kết quả)
- Trong **Postman**, báo cáo sẽ được thêm thành công vào SQL Server (Status 200).
- Chuyển sang cửa sổ trang Web **HTML ở Bước 4.3**, bạn sẽ thấy tự động nhảy ra một dòng thông báo Real-time: `🔔 CÓ THÔNG BÁO MỚI:` báo hiệu hệ thống đã hoạt động khép kín thành công!
