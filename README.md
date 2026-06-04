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

## 🔌 Danh sách Cổng Dịch vụ (Port Mappings)

Khi khởi chạy hệ thống, các dịch vụ sẽ được lắng nghe ở các cổng sau:

| Dịch vụ | Cổng chạy Container | Cổng chạy Localhost | Mô tả |
| :--- | :--- | :--- | :--- |
| **ApiGateway** | `8080` | `5000` | Cổng ngõ chính nhận toàn bộ REST API request |
| **Auth.API** | `8080` | `5001` | Quản lý Đăng ký / Đăng nhập / Cấp Token JWT |
| **Club.API** | `8080` & `9001` | `5002` & `9001` | Quản lý thông tin CLB & cung cấp gRPC Server |
| **Report.API** | `8080` | `5003` | Nộp/xử lý báo cáo CLB |
| **Notification.API** | `8080` | `5004` | Nhận sự kiện Redis để xử lý gửi thông báo |
| **SQL Server** | `1433` | `1433` | Cơ sở dữ liệu SQL Server chung |
| **Redis** | `6379` | `6379` | Message Broker cho Pub/Sub |

---

## 🚀 Hướng dẫn khởi chạy bằng Docker

Để chạy thử toàn bộ hệ thống (bao gồm database, message broker và tất cả các services), bạn chỉ cần thực hiện 1 câu lệnh duy nhất từ thư mục gốc dự án:

```bash
docker-compose up --build
```

### Kiểm tra hệ thống:
1. **API Gateway:** Truy cập qua `http://localhost:5000/gateway/auth/me` để kiểm tra gateway kết nối với Auth API.
2. **Swagger Docs:** Xem tài liệu API của từng service đơn lẻ tại:
   * Auth Service: `http://localhost:5001/swagger/index.html`
   * Report Service: `http://localhost:5003/swagger/index.html`
   * Club Service: `http://localhost:5002/swagger/index.html`
