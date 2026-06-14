# Kế hoạch Milestone 6: Web Frontend (Giao diện người dùng)

Sau khi hoàn thiện toàn bộ hệ thống Backend bằng kiến trúc Microservices và gộp chung vào API Gateway, **Milestone 6** là chặng đường cuối cùng để thổi hồn vào dự án: Xây dựng một giao diện Web trực quan, đẹp mắt và hiện đại để người dùng tương tác với hệ thống.

---

## 1. Mục tiêu của Milestone 6
- Chuyển đổi các thao tác gọi API khô khan trên Postman thành một website hoàn chỉnh có nút bấm, form nhập liệu.
- Mang lại trải nghiệm **"WOW"** cho người dùng thông qua các thiết kế hiện đại (Aesthetics).
- Kết nối và kiểm chứng sức mạnh của hệ thống Real-time Notification thông qua tín hiệu hình ảnh (Toast, Popup, Bell icon).

---

## 2. Công nghệ đề xuất
Bạn có thể tự do lựa chọn công nghệ phù hợp với năng lực của nhóm:

- **Lựa chọn 1 (Dễ, nhanh): HTML, CSS, Vanilla JavaScript.**
  - Phù hợp để code chay, không cần cài đặt môi trường phức tạp.
  - Vẫn có thể làm ra giao diện cực đẹp nếu biết kết hợp thư viện CSS.
- **Lựa chọn 2 (Chuyên nghiệp, xịn xò): ReactJS, VueJS hoặc Next.js.**
  - Hệ thống Single Page Application (SPA), chuyển trang không cần load lại web.
  - Dễ quản lý component và state (ví dụ quản lý Token đăng nhập, list thông báo).

---

## 3. Yêu cầu thiết kế (Aesthetics Requirements)
Vì đây là hệ thống quản lý câu lạc bộ của môi trường FPTU trẻ trung, giao diện KHÔNG ĐƯỢC thiết kế sơ sài. Bắt buộc áp dụng các tiêu chuẩn thiết kế hiện đại:
- **Theme:** Hỗ trợ Dark Mode (Giao diện tối) mạnh mẽ, hoặc sử dụng hệ thống màu gradient bắt mắt.
- **Glassmorphism:** Hiệu ứng kính mờ cho các thẻ Card, Popup thông báo.
- **Typography:** Sử dụng Font chữ hiện đại từ Google Fonts (như `Inter`, `Roboto`, hoặc `Outfit`).
- **Micro-animations:** Nút bấm có hiệu ứng hover mượt mà, thông báo trượt (slide-in) từ góc màn hình ra, danh sách tải lên có hiệu ứng mờ dần (fade-in).

---

## 4. Các màn hình chính (Core Screens)

### 1. Màn hình Đăng nhập / Đăng ký
- **Chức năng:** Gửi yêu cầu tới `POST /gateway/auth/login`. Lấy JWT Token lưu vào `localStorage` hoặc `cookie`.
- **Thiết kế:** Split-screen (chia đôi màn hình), một bên là hình ảnh sự kiện CLB sôi động, một bên là Form đăng nhập kính mờ.

### 2. Bảng điều khiển (Dashboard / Clubs)
- **Chức năng:** Gửi yêu cầu tới `GET /gateway/clubs/`. Hiển thị danh sách câu lạc bộ dưới dạng Grid Cards.
- **Thiết kế:** Mỗi Card có Logo CLB, tên, số lượng thành viên. Bấm vào Card để xem chi tiết.

### 3. Màn hình Báo cáo (Report Management)
- **Chức năng:** Form cho phép nhập Title, Content, Type. Gọi `POST /gateway/reports`.
- **Thiết kế:** Trình soạn thảo văn bản nhìn sạch sẽ. Một bảng danh sách hiển thị các báo cáo đã nộp kèm Status (Pending, Approved).

### 4. Hệ thống Thông báo (Notification)
- **Chức năng:** Tích hợp SignalR vào Frontend (`@microsoft/signalr`). Khi Server có biến, trình duyệt sẽ nhận được chuỗi JSON.
- **Thiết kế:** Một cái Icon Cái Chuông (Bell) ở góc trên bên phải màn hình. Khi có thông báo, chuông sẽ lắc (CSS Animation), hiển thị con số màu đỏ. Đồng thời có một Toast Notification (giống thông báo của Facebook) trượt ra từ góc dưới màn hình.

---

## 5. Các bước triển khai (Workflow)
1. **Khởi tạo Project:** Tạo thư mục `frontend` bên cạnh thư mục `src`. Dựng khung sườn HTML/CSS.
2. **Cấu hình Axios / Fetch API:** Viết file cấu hình gốc để mọi request gửi đi đều tự động đính kèm `Bearer Token` vào Header. Tất cả request đều bắn về **Cổng 5000** (API Gateway).
3. **Làm màn Auth trước:** Xây form Đăng nhập, lưu Token, điều hướng người dùng sang trang Chủ.
4. **Lắp ráp SignalR:** Ngay khi vào trang Chủ, khởi tạo kết nối WebSocket với `/gateway/hubs/notification`.
5. **Hoàn thiện các màn hình còn lại:** Làm tính năng hiển thị Club và Form Report.

Chúc các bạn code vui vẻ ở Milestone cuối cùng!
