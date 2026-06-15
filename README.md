# FPTU Club Report System

Hệ thống quản lý và báo cáo hoạt động câu lạc bộ tại Đại học FPT (FPTU). Dự án được phát triển bằng công nghệ **.NET 8** theo kiến trúc **Microservices** kết hợp **Clean Architecture** ở mỗi dịch vụ độc lập.

---

## 🛠️ Công nghệ sử dụng (Tech Stack)

* **Backend Framework:** .NET 8 (ASP.NET Core, Class Libraries)
* **API Gateway:** Ocelot Gateway (định tuyến và xác thực tập trung)
* **Giao tiếp Đồng bộ (Sync):** gRPC (Client-Server)
* **Giao tiếp Bất đồng bộ (Async):** Redis Pub/Sub
* **Hạ tầng cơ sở dữ liệu:** Microsoft SQL Server 2022, Redis Cache/Message Broker
* **Ảo hóa & Vận hành:** Docker, Docker Compose

---

## 📐 Cấu trúc Kiến trúc (Architecture)

Mỗi Microservice (ví dụ: `Report.API`) được cấu trúc thành 4 lớp độc lập tuân thủ nghiêm ngặt quy tắc của **Clean Architecture**:

1. **Domain Layer:** Lớp trung tâm chứa Entity, Enums, Value Objects và các Business Rules thuần túy.
2. **Application Layer:** Chứa Interface, CQRS Commands/Queries (sử dụng MediatR), DTOs và Event Contracts.
3. **Infrastructure Layer:** Cài đặt công nghệ bên ngoài bao gồm DB Context (EF Core), gRPC Client và Redis Publisher.
4. **API Layer:** Điểm khởi chạy của dịch vụ chứa các HTTP Controllers và Middlewares quản lý vòng đời request.

---

## 🔌 Danh sách Cổng Dịch vụ & Swagger Links (Port Mappings)

Khi khởi chạy hệ thống, các dịch vụ sẽ được lắng nghe ở các cổng sau:

| Dịch vụ | Cổng Container | Cổng Localhost | Tài liệu Swagger (Click để mở) | Mô tả |
| :--- | :--- | :--- | :--- | :--- |
| **ApiGateway** | `8080` | `5000` | *Không có* | Cổng ngõ chính định tuyến toàn bộ REST API request |
| **Auth.API** | `8080` | `5001` | [Auth Swagger Docs](http://localhost:5001/swagger/index.html) | Đăng ký, đăng nhập, đổi mật khẩu, quên mật khẩu |
| **Club.API** | `8080` & `9001` | `5002` & `9001` | [Club Swagger Docs](http://localhost:5002/swagger/index.html) | Quản lý câu lạc bộ, thành viên & sự kiện (gRPC Port: 9001) |
| **Report.API** | `8080` | `5003` | [Report Swagger Docs](http://localhost:5003/swagger/index.html) | Nộp và phê duyệt báo cáo CLB |
| **Notification.API** | `8080` | `5004` | [Notification Swagger Docs](http://localhost:5004/swagger/index.html) | Đẩy thông báo thời gian thực qua SignalR |
| **SQL Server** | `1433` | `1433` | *Kết nối: localhost,1433 (sa / YourStrong!Passw0rd)* | Cơ sở dữ liệu SQL Server lưu trữ dữ liệu bền vững |
| **Redis** | `6379` | `6379` | *Kết nối: localhost,6379* | Message Broker phục vụ truyền tin Pub/Sub |

---

## 👥 Tài Khoản Thử Nghiệm Mặc Định (Default Seeded Users)
Bạn có thể sử dụng các tài khoản có sẵn dưới đây để đăng nhập qua Auth API (Mật khẩu chung cho tất cả tài khoản là **`Fptu@123`**):

| Email | Họ và Tên | Vai Trò (Role) | Chức năng thử nghiệm |
| :--- | :--- | :--- | :--- |
| `admin@fpt.edu.vn` | BQL CLB FPTU (Admin) | **`Admin`** | Toàn quyền quản trị, duyệt báo cáo và CLB |
| `advisor1@fpt.edu.vn` | Nguyen Van A (Cố vấn) | **`Advisor`** | Giáo viên cố vấn, có quyền phê duyệt báo cáo |
| `manager1@fpt.edu.vn` | Tran Thi B (Trưởng CLB) | **`ClubManager`** | Chủ nhiệm CLB F-Code, nộp báo cáo và tạo sự kiện |
| `student1@fpt.edu.vn` | Le Van C (Thành viên) | **`Student`** | Sinh viên phổ thông, xin tham gia CLB |

---

## 🚀 Hướng dẫn khởi chạy bằng Docker

Để chạy thử toàn bộ hệ thống (bao gồm database, message broker và tất cả các services), bạn chỉ cần thực hiện 1 câu lệnh duy nhất từ thư mục gốc dự án:

```bash
docker-compose up --build
```

### Kiểm tra hệ thống:
1. **API Gateway:** Truy cập qua `http://localhost:5000/gateway/auth/me` để kiểm tra gateway kết nối với Auth API.
2. **Swagger Docs:** Xem và gọi thử các API trực tiếp thông qua các đường dẫn Swagger ở bảng cổng dịch vụ phía trên.
3. **SignalR Hub Test (Nhận thông báo realtime):** Mở trực tiếp file `test-signalr-gateway.html` trên trình duyệt, dán token của tài khoản đang cần nhận thông báo rồi nhấn kết nối để nghe sự kiện từ gateway.

