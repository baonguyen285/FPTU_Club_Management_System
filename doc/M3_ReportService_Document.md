# Tài liệu Kỹ thuật & Hướng dẫn kiểm thử - M3 (Report Service)

Tài liệu này tổng hợp toàn bộ các tính năng đã được xây dựng cho **Milestone 3 (Report Service)**, mô tả luồng hoạt động của kiến trúc Clean Architecture kết hợp CQRS, và hướng dẫn cách test (kiểm thử) các chức năng này.

---

## 1. Tổng quan Kiến trúc

Report Service được xây dựng theo **Clean Architecture**, bao gồm 4 tầng (Layer):
1. **Report.Domain**: Định nghĩa Entities (`Report`, `ReportAttachment`) và Enums (`ReportStatus`, `ReportType`). Tầng này thuần tuý là logic lõi, không phụ thuộc vào framework.
2. **Report.Application**: 
   - Định nghĩa interface `IReportRepository`, `IReportUnitOfWork`, và `IClubGrpcClient`.
   - Cấu hình AutoMapper (`ReportMappingProfile`).
   - Xây dựng kiến trúc **CQRS (Command/Query Responsibility Segregation)** thông qua thư viện `MediatR` với các features: `CreateReport`, `UpdateReport`, `ReviewReport`, `GetReportsByClubId`, `GetReportById`.
3. **Report.Infrastructure**:
   - Khởi tạo Database Context (`ReportDbContext`) thông qua Entity Framework Core (SQL Server).
   - Thực thi Repository (`ReportRepository`) và UnitOfWork (`ReportUnitOfWork`).
   - Cấu hình **gRPC Client** (`ClubGrpcClient`) để kết nối tới `ClubService` nhằm xác thực quyền quản lý Câu lạc bộ.
4. **Report.API**:
   - Các `Controllers` tiếp nhận request HTTP (`ReportsController`).
   - Cấu hình Middleware xử lý lỗi (`ExceptionHandlingMiddleware`) dùng chung từ `Shared.Kernel`.
   - Xác thực danh tính qua **JWT Authentication**.

## 2. Kết nối Microservices (gRPC)

Bên cạnh việc độc lập lưu trữ dữ liệu báo cáo, `Report.API` giao tiếp trực tiếp với `Club.API` thông qua giao thức **gRPC** (siêu tốc, độ trễ thấp).
- **Mục đích**: Khi một người dùng (Manager/President) cố gắng xét duyệt một báo cáo, `ReportService` cần biết chính xác user đó có đúng là quản lý của Câu lạc bộ tương ứng hay không.
- **Cách hoạt động**: `ReportService` gửi yêu cầu (gồm `ClubId` và `UserId`) qua gRPC sang cổng `9001` của `ClubService`. `ClubService` tra cứu Database nội bộ và trả về kết quả `True/False`. Nhờ đó đảm bảo tính bảo mật và toàn vẹn dữ liệu cho từng CLB.

## 3. Các API Endpoints & CQRS Features

| HTTP Method | Endpoint | CQRS Handler | Chức năng | Yêu cầu Phân quyền |
| ----------- | -------- | ------------ | --------- | ---------- |
| `POST` | `/api/v1/Reports` | `CreateReportCommand` | Tạo báo cáo mới | `[Authorize]` (Token hợp lệ) |
| `PUT` | `/api/v1/Reports/{id}` | `UpdateReportCommand` | Chỉnh sửa nội dung báo cáo | `[Authorize]` (Chỉ người tạo báo cáo) |
| `PUT` | `/api/v1/Reports/{id}/review`| `ReviewReportCommand` | Duyệt/Từ chối báo cáo | `[Authorize]` (Chỉ Club Manager/President) |
| `GET` | `/api/v1/Reports/club/{clubId}` | `GetReportsByClubIdQuery` | Tra cứu danh sách báo cáo (Hỗ trợ filter `type` & `status`) | Không yêu cầu |
| `GET` | `/api/v1/Reports/{id}` | `GetReportByIdQuery` | Xem chi tiết một báo cáo cụ thể | Không yêu cầu |

---

## 4. Hướng dẫn Kiểm thử (Test) qua Swagger

### 4.1. Chuẩn bị (Authentication & Data)
1. Đảm bảo chạy đồng thời cả 3 service: `Auth.API`, `Club.API`, `Report.API` (Nên chạy bằng lệnh `docker-compose up --build`).
2. Qua Swagger của **Auth.API**, gọi API Login để lấy `Token JWT` cho tài khoản.
3. Mở Swagger của **Report.API**, bấm nút **Authorize 🔓**, dán Token vào để mở khoá bảo mật.
4. *Lưu ý*: Để test API Duyệt báo cáo, tài khoản đang dùng phải được cấu hình quyền Manager ở `Club.API` (Role = 1, Status = 1).

### 4.2. Test Tạo Báo cáo (Create Report)
1. Chọn API `POST /api/v1/Reports`.
2. Điền Request Body với mẫu:
```json
{
  "clubId": "99999999-9999-9999-9999-999999999999",
  "title": "Báo cáo hoạt động sự kiện tháng 6",
  "content": "Hoàn thành xuất sắc 100% mục tiêu đã đề ra.",
  "type": 2,
  "attachments": []
}
```
3. Nhấn **Execute**. Lưu lại mã `id` báo cáo được trả về.

### 4.3. Test Duyệt Báo cáo (Review Report)
*Yêu cầu: Bắt buộc dùng Token JWT của Manager/President CLB đó.*
1. Chọn API `PUT /api/v1/Reports/{id}/review`.
2. Dán mã báo cáo vừa tạo vào ô `id`.
3. Điền Request Body:
```json
{
  "isApproved": true,
  "reviewNote": "Đã xem kỹ, đồng ý duyệt báo cáo này."
}
```
4. Nhấn **Execute**. Kết quả sẽ trả về báo cáo được đổi trạng thái sang `Approved`.

### 4.4. Test Lấy Danh Sách & Lọc Báo Cáo
1. Chọn API `GET /api/v1/Reports/club/{clubId}`.
2. Nhập `clubId` = `99999999-9999-9999-9999-999999999999`.
3. Thử sử dụng các bộ lọc (query params): Nhập `status = 2` (Approved) và `type = 2` (Activity).
4. Nhấn **Execute** để xem hệ thống có lọc chính xác báo cáo vừa tạo ở trên hay không.
