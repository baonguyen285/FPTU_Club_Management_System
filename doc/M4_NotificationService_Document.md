# Milestone 4: Notification Service

## 1. Giới thiệu (Overview)
Notification Service chịu trách nhiệm xử lý các luồng thông báo thời gian thực (Real-time) trong hệ thống FPTU Club Management. Nó hoạt động như một thành phần độc lập, lắng nghe các sự kiện từ Message Broker (Redis) và đẩy thông báo trực tiếp đến trình duyệt của người dùng thông qua SignalR.

## 2. Công nghệ sử dụng
- **Framework:** .NET 8 (ASP.NET Core Web API)
- **Kiến trúc:** Clean Architecture (Domain, Application, Infrastructure, API)
- **Real-time:** ASP.NET Core SignalR
- **Message Broker:** Redis Pub/Sub (Sử dụng StackExchange.Redis)
- **Database:** Tạm thời sử dụng In-Memory DB hoặc SQL Server cho việc lưu trữ lịch sử thông báo (Notification Entity).

## 3. Cấu trúc các lớp (Clean Architecture)

### 3.1 Domain Layer
- **Entities:**
  - `Notification`: Chứa các trường cơ bản như `Id`, `UserId`, `Title`, `Message`, `Type`, `IsRead`, `CreatedAt`.
- **Enums:**
  - `NotificationType`: Các loại thông báo (ReportSubmitted, ClubEventCreated, SystemAlert, v.v...).

### 3.2 Application Layer
- **Interfaces:**
  - `INotificationRepository`: Interface giao tiếp với Database để CRUD thông báo.
  - `IUnitOfWork`: Quản lý transaction.

### 3.3 Infrastructure Layer
- **SignalR Hub (`NotificationHub`):**
  - Quản lý các kết nối WebSocket từ Client.
  - Phân luồng người dùng vào các Group dựa trên `UserId` (từ JWT Token).
- **Redis Event Consumer (`RedisEventConsumer`):**
  - Kế thừa `BackgroundService` chạy ngầm.
  - Lắng nghe kênh `report-events-channel`.
  - Khi có sự kiện (ví dụ: Tạo báo cáo mới), consumer sẽ:
    1. Ghi thông báo vào Database.
    2. Gọi `IHubContext` để bắn thông báo qua WebSocket (SignalR) tới đúng Manager/User cần nhận.

### 3.4 API Layer
- **Program.cs:**
  - Đăng ký SignalR (`builder.Services.AddSignalR()`).
  - Đăng ký Redis Multiplexer.
  - Khởi chạy Hosted Service (`RedisEventConsumer`).
  - Phân luồng Endpoint (`app.MapHub<NotificationHub>("/hubs/notification")`).

## 4. Luồng hoạt động (Workflow)
1. **Phát sự kiện (Từ Report Service):**
   - Khi một báo cáo mới được Submit thành công, `ReportService` sử dụng `RedisEventPublisher` gửi một chuỗi JSON (chứa ReportId, ClubId, Title) vào Redis Channel.
2. **Nhận sự kiện (Tại Notification Service):**
   - `RedisEventConsumer` (đang trực chờ sẵn) bắt được chuỗi JSON này, phân tích dữ liệu.
3. **Phát sóng thời gian thực (Tới Client):**
   - `Notification Service` gọi `NotificationHub`, đẩy thông điệp cảnh báo tới trình duyệt web đang kết nối WebSocket (hoặc SignalR Client).
   - Trình duyệt sẽ hiển thị Popup/Toast thông báo cho người dùng.

## 5. Danh sách API / Hub Endpoint
- **SignalR Hub URL:** `ws://localhost:5288/hubs/notification` (Sử dụng JWT Bearer token được truyền qua query `?access_token=...`)
