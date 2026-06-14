Chào bạn, tôi muốn bắt đầu xây dựng Milestone 6: Web Frontend cho dự án "FPTU Club Management System".

1. Bối cảnh dự án (Backend Context):

Hệ thống Backend đã hoàn thiện theo kiến trúc Microservices (.NET 8).
Đang chạy bằng Docker Compose tại môi trường Local.
Toàn bộ các API đều được gọi thông qua API Gateway ở cổng 5000 (Base URL: http://localhost:5000/gateway/).
Dự án có sử dụng SignalR để bắn thông báo Real-time (WebSocket URL: ws://localhost:5000/gateway/hubs/notification).
2. Công nghệ Frontend yêu cầu:

Sử dụng: HTML, CSS và Vanilla Javascript.
Gọi API bằng fetch hoặc axios.
Kết nối SignalR bằng thư viện @microsoft/signalr qua CDN.
3. Yêu cầu thiết kế (Aesthetics & UX/UI):

Giao diện: Phải hiện đại, trẻ trung mang phong cách đại học FPT.
Bắt buộc có: Giao diện tối (Dark Mode) hoặc Gradient tinh tế.
Hiệu ứng: Sử dụng hiệu ứng kính mờ (Glassmorphism) cho các form và popup. Các nút bấm phải có Micro-animations (hover mượt, click có hiệu ứng).
4. Yêu cầu các màn hình cần làm (Làm theo thứ tự):

👉 Bước 1: Màn hình Đăng nhập (Login)

Thiết kế một Form nhập Email & Password có kính mờ (Glassmorphism).
Khi submit, gọi POST http://localhost:5000/gateway/auth/login.
Nếu thành công, bóc tách JWT Token từ Response và lưu vào localStorage. Điều hướng sang trang Dashboard.
👉 Bước 2: Bảng điều khiển (Dashboard)

Giao diện chính sau khi đăng nhập. Có Sidebar hoặc Header điều hướng.
Ở góc trên màn hình phải có một cái Icon Cái Chuông (Notification Bell).
Ngay khi load xong Dashboard, dùng Token trong localStorage để kết nối WebSocket tới: ws://localhost:5000/gateway/hubs/notification?access_token={token}.
Khi SignalR nhận sự kiện ReceiveNotification, làm hiệu ứng chuông rung lên + hiện chấm đỏ, đồng thời trượt ra một cái "Toast Notification" nhỏ gọn đẹp mắt dưới góc màn hình.
👉 Bước 3: Màn hình Quản lý Báo cáo (Report)

Cho phép người dùng điền thông tin: ClubId (chuỗi Guid), Title, Content, Type.
Submit gọi API POST http://localhost:5000/gateway/reports đính kèm Header Authorization: Bearer {token}.
Hãy đóng vai một chuyên gia Web Frontend lão luyện. Lên kế hoạch cấu trúc thư mục (HTML/CSS/JS) cho tôi và bắt đầu tạo cho tôi file index.html (Màn hình Đăng nhập) kèm CSS đẹp nhất có thể nhé!