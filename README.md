# FPTU Club Report System

> Runtime fixture note (2026-07-26): source seed defines `student1@fpt.edu.vn` as a canonical `Student`/Member, but the current shared database was observed with a drifted `ClubManager`/Treasurer assignment. Do not use that runtime row as Student demo evidence until it is normalized on an approved or disposable fixture.

For isolated role-based E2E verification, use `scripts/e2e/setup-demo-fixtures.ps1`. It creates fixed-ID `demo.*` users and a `DEMO-E2E Club` idempotently without embedding a password or token. Always finish with `scripts/e2e/cleanup-demo-fixtures.ps1`; cleanup is scoped to the known demo IDs, club ID, and `E2E-`/`DEMO-` resources.

Hệ thống quản lý và báo cáo hoạt động câu lạc bộ tại Đại học FPT (FPTU). Dự án được phát triển bằng công nghệ **.NET 8** theo kiến trúc **Microservices** kết hợp **Clean Architecture** ở mỗi dịch vụ độc lập.

---

## 🛠️ Công nghệ sử dụng (Tech Stack)

* **Backend Framework:** .NET 8 (ASP.NET Core, Class Libraries)
* **API Gateway:** Ocelot Gateway (định tuyến và xác thực tập trung)
* **Giao tiếp Đồng bộ (Sync):** gRPC (Client-Server)
* **Giao tiếp Bất đồng bộ (Async):** Redis Streams với transactional outbox, consumer group, pending recovery, retry và DLQ. Pub/Sub chỉ là legacy bridge bị tắt mặc định.
* **Hạ tầng cơ sở dữ liệu:** Microsoft SQL Server 2022 và Redis message broker. Redis hiện chưa được dùng làm application cache.
* **Background jobs:** Hangfire SQL storage trong Report Service; `PendingReportReminderJob` chạy lúc 08:00 theo múi giờ `Asia/Ho_Chi_Minh`.
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
| **Redis** | `6379` | `6379` | *Kết nối: localhost,6379* | Redis Streams message broker; không phải application cache |

---

## 👥 Tài Khoản Thử Nghiệm Mặc Định (Default Seeded Users)
Bạn có thể sử dụng các tài khoản có sẵn dưới đây để đăng nhập qua Auth API (Mật khẩu chung cho tất cả tài khoản là **`Fptu@123`**):

| Email | Họ và Tên | Vai Trò (Role) | Chức năng thử nghiệm |
| :--- | :--- | :--- | :--- |
| `admin@fpt.edu.vn` | BQL CLB FPTU (Admin) | **`StudentAffairsAdmin`** | Toàn quyền quản trị, duyệt báo cáo và CLB |
| `advisor1@fpt.edu.vn` | Nguyen Van A (Cố vấn) | **`StudentAffairsAdmin`** | Tài khoản quản trị tương thích dữ liệu cũ |
| `manager1@fpt.edu.vn` | Tran Thi B (Trưởng CLB) | **`ClubManager`** | Chủ nhiệm CLB F-Code, nộp báo cáo và tạo sự kiện |
| `treasurer1@fpt.edu.vn` | Pham Minh D (Thủ quỹ) | **`ClubManager`** | Treasurer của CLB F-Code, quản lý tài chính |
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

## Semester foundation

Report/KPI Service owns Semester persistence. Public routes use
`/gateway/kpi/semesters`; only `StudentAffairsAdmin` may create, update, activate, or close.
Semester codes are unique and activation guarantees at most one Active semester.
BE-8 binds new reports and KPI rules/history to Semester while preserving nullable legacy report references.

## BE-8 revision, finance settlement and KPI

- Reports use explicit Draft/submit/revision/resubmit/review transitions and expose read-only history.
- Finance exposes proposal settlement, transaction ledger and club balance. Receipt URL metadata is required; exact-club Treasurer owns `ManageFinance`.
- KPI leaderboard requires `semesterId` and sums `KpiScoreHistories`; admin manual adjustment is the supported BE-8 scoring source.
- Redis streams include report workflow events and `BudgetApprovedV1`/`BudgetSettledV1`.

## BE-9 frontend integration

- Frontend compliance profile dùng Gateway tại `http://localhost:5000` và không fallback sang mock.
- Access token chỉ giữ trong memory; refresh token khôi phục phiên bằng single-flight refresh.
- SignalR kết nối qua `/gateway/hubs/notification`.
- UI thật đã có cho users, clubs/membership, activities, report revision/history, Finance, KPI/Semester và notifications/broadcast.
- Dashboard tổng hợp từ các API hiện có, không hiển thị metric hard-code.

### Build và test

```powershell
dotnet build FPTU_Club_Report_System.sln
dotnet test FPTU_Club_Report_System.sln --no-build
docker compose -p fptu-p0-verify up --build -d
docker compose -p fptu-p0-verify ps --all
```

Frontend:

```powershell
npm run build
```

### Giới hạn

File upload receipt, export PDF/Excel, Redis cache, full dashboard metrics và consumer cho mọi event được hoãn. Mixed database initialization và package warnings cũ chưa được refactor trong BE-9.

# Corrected Club demo flow (26/07/2026)

1. A Student opens `/club-applications` and submits a proposal.
2. Student Affairs Admin opens `/admin/club-applications` and approves or rejects it.
3. Approval creates one Active Club and one approved ClubLeader membership for the applicant.
4. The applicant can manage join requests, events and reports for that exact Club without requiring the deprecated `ClubManager` compatibility role.

The existing Admin create-club endpoint is support/bootstrap only. Application approval/rejection notifications use `fptu.club.events.workflow.v1`; invitation and remaining membership notifications are deferred. Do not use shared `student1` for disposable E2E data; use `E2E-`/`DEMO-` resources.
