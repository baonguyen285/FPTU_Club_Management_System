# Tài liệu Kỹ thuật & Hướng dẫn kiểm thử - M2 (Club Service)

Tài liệu này tổng hợp toàn bộ các tính năng đã được xây dựng cho **Milestone 2 (Club Service)**, mô tả luồng hoạt động của kiến trúc Clean Architecture kết hợp CQRS, và hướng dẫn cách test (kiểm thử) các chức năng này.

---

## 1. Tổng quan Kiến trúc

Club Service được xây dựng theo **Clean Architecture**, bao gồm 4 tầng (Layer):
1. **Club.Domain**: Định nghĩa Entities (`Club`, `ClubMember`, `Event`) và Enums (`ClubRole`, `ClubStatus`, `EventStatus`, `MembershipStatus`). Tầng này không phụ thuộc vào bất kỳ công nghệ hay framework nào khác.
2. **Club.Application**: 
   - Định nghĩa các interface: `IClubRepository`, `IUnitOfWork`.
   - Cấu hình AutoMapper (`MappingProfile`).
   - Xây dựng **CQRS (Command/Query Responsibility Segregation)** thông qua `MediatR` chia logic thành các luồng thao tác: CreateClub (Ghi), GetClubs (Đọc), v.v.
3. **Club.Infrastructure**:
   - Khởi tạo Database Context (`ClubDbContext`) thông qua Entity Framework Core.
   - Thể hiện thực tế của `IClubRepository` (thực thi các query vào DB) và `IUnitOfWork` (quản lý transaction `SaveChangesAsync`).
4. **Club.API**:
   - `Controllers` chứa các Restful APIs nhận request từ HTTP.
   - `GrpcServices` chứa logic để các microservices khác giao tiếp nội bộ (thông qua giao thức gRPC).
   - `Program.cs` thiết lập toàn bộ Dependency Injection.

---

## 2. Các chức năng đã xây dựng & Luồng hoạt động

### A. Nhóm chức năng Câu lạc bộ (Clubs)

#### 1. Lấy danh sách CLB (GET `/api/v1/clubs`)
- **Mô tả**: Liệt kê danh sách tất cả các câu lạc bộ hiện có trong hệ thống.
- **Luồng hoạt động**: `GetClubsQuery` ➡️ `IUnitOfWork.Clubs.GetAllAsync()`

#### 2. Xem chi tiết CLB (GET `/api/v1/clubs/{id}`)
- **Mô tả**: Xem thông tin chi tiết của 1 CLB cụ thể (kèm danh sách members và events).
- **Luồng hoạt động**: `GetClubByIdQuery` ➡️ `IUnitOfWork.Clubs.GetByIdAsync()`

#### 3. Tạo CLB mới (POST `/api/v1/clubs`) - Cần đăng nhập
- **Mô tả**: Tạo một CLB mới với trạng thái mặc định là `PendingApproval`.
- **Luồng hoạt động**: `CreateClubCommand` ➡️ `IUnitOfWork.Clubs.AddAsync()`

#### 4. Cập nhật CLB (PUT `/api/v1/clubs/{id}`) - Cần đăng nhập
- **Mô tả**: Cập nhật thông tin CLB (Tên, Mô tả, Logo).
- **Luồng hoạt động**: `UpdateClubCommand` ➡️ `IUnitOfWork.Clubs.Update()`

#### 5. Xóa mềm CLB (DELETE `/api/v1/clubs/{id}`) - Cần đăng nhập
- **Mô tả**: Đánh dấu CLB ngừng hoạt động (`IsActive = false`) thay vì xóa mất dữ liệu.
- **Luồng hoạt động**: `DeleteClubCommand` ➡️ `IUnitOfWork.Clubs.Update()`

#### 6. Phê duyệt/Thay đổi trạng thái CLB (PUT `/api/v1/clubs/{id}/review`) - Chỉ Admin, Advisor
- **Mô tả**: Duyệt câu lạc bộ mới tạo (PendingApproval -> Active) hoặc thay đổi trạng thái hoạt động (Active, Suspended, Inactive).
- **Luồng hoạt động**: `ReviewClubCommand` ➡️ `IUnitOfWork.Clubs.Update()`

### B. Nhóm chức năng Thành viên (Members)

#### 1. Đăng ký tham gia CLB (POST `/api/v1/clubs/{id}/members`) - Cần đăng nhập
- **Mô tả**: Một user muốn nộp đơn xin gia nhập CLB (Status = Pending). 
- **Luồng hoạt động**: `JoinClubCommand` ➡️ `IUnitOfWork.Clubs.AddMemberAsync()`

#### 2. Lấy danh sách thành viên (GET `/api/v1/clubs/{id}/members`)
- **Mô tả**: Xem danh sách các thành viên thuộc CLB.
- **Luồng hoạt động**: `GetClubMembersQuery` ➡️ `IUnitOfWork.Clubs.GetMembersByClubAsync()`

#### 3. Thay đổi vai trò / Duyệt thành viên (PUT `/api/v1/clubs/{id}/members/{userId}/role`) - Cần đăng nhập
- **Mô tả**: Duyệt đơn tham gia (Pending -> Approved) hoặc thăng chức Manager/President.
- **Luồng hoạt động**: `UpdateMemberRoleCommand` ➡️ `IUnitOfWork.Clubs.UpdateMember()`

#### 4. Đuổi / Rời CLB (DELETE `/api/v1/clubs/{id}/members/{userId}`) - Cần đăng nhập
- **Mô tả**: Thay đổi trạng thái thành viên sang `Left` (Đã rời đi).
- **Luồng hoạt động**: `RemoveMemberCommand` ➡️ `IUnitOfWork.Clubs.UpdateMember()`

### C. Nhóm chức năng Sự kiện (Events)

#### 1. Tạo sự kiện (POST `/api/v1/events`) - Cần đăng nhập
- **Mô tả**: Tạo một event cho CLB.
- **Luồng hoạt động**: `CreateEventCommand` ➡️ `IUnitOfWork.Clubs.AddEventAsync()`

#### 2. Lấy sự kiện theo CLB (GET `/api/v1/events/club/{clubId}`)
- **Mô tả**: Lấy toàn bộ các sự kiện thuộc một CLB cụ thể.
- **Luồng hoạt động**: `GetEventsByClubQuery` ➡️ `IUnitOfWork.Clubs.GetEventsByClubAsync()`

#### 3. Cập nhật sự kiện (PUT `/api/v1/events/{id}`) - Cần đăng nhập
- **Mô tả**: Chỉnh sửa thông tin sự kiện.
- **Luồng hoạt động**: `UpdateEventCommand` ➡️ `IUnitOfWork.Clubs.UpdateEvent()`

#### 4. Hủy sự kiện - Xóa mềm (DELETE `/api/v1/events/{id}/cancel`) - Cần đăng nhập
- **Mô tả**: Hủy sự kiện (`Status = Cancelled`, `IsActive = false`).
- **Luồng hoạt động**: `SoftDeleteEventCommand` ➡️ `IUnitOfWork.Clubs.UpdateEvent()`

#### 5. Xóa vĩnh viễn sự kiện (DELETE `/api/v1/events/{id}/permanent`) - Cần đăng nhập
- **Mô tả**: Xóa vĩnh viễn dữ liệu rác khỏi Database.
- **Luồng hoạt động**: `HardDeleteEventCommand` ➡️ `IUnitOfWork.Clubs.DeleteEvent()`

### D. Nhóm chức năng giao tiếp nội bộ (gRPC Server)

Được định nghĩa tại `Shared.Kernel/Grpc/Protos/club.proto` và triển khai tại `ClubGrpcServiceImpl.cs`.
Các microservices khác (như Report Service, Notification Service) sẽ gọi các hàm này:
- **`CheckClubExists`**: Kiểm tra xem ID của CLB gửi sang có tồn tại và đang hoạt động (Active) hay không.
- **`GetClubInfo`**: Trả về cơ bản Id, Name của CLB.
- **`IsClubManager`**: Kiểm tra xem User gửi sang có phải là Manager hoặc President của CLB này không (Status của member phải là Approved). Dùng để Report Service check quyền duyệt báo cáo.

---

## 3. Hướng dẫn Kiểm thử (Testing)

### Cách 1: Chạy và Test thông qua Swagger
1. Đứng ở thư mục gốc dự án (chứa file `docker-compose.yml`), mở terminal chạy lệnh:
   ```bash
   docker-compose up --build
   ```
2. Đợi cho đến khi các container (đặc biệt là `fptu-club`) khởi chạy xong.
3. Mở trình duyệt và truy cập vào Swagger của Club Service:
   - **http://localhost:5002/swagger/index.html**

4. **Test GET `/api/v1/clubs`**:
   - Chọn API GET `/api/v1/clubs`, nhấn *Try it out* -> *Execute*.
   - Bạn sẽ thấy kết quả trả về `200 OK` chứa ít nhất 1 Club mặc định (FPTU Software Engineering Club) đã được Seed trong DB.

5. **Test tính năng chặn chứng thực (Authentication)**:
   - Thử gọi POST `/api/v1/clubs` mà không truyền Token -> Bạn sẽ nhận được HTTP 401 (Unauthorized).
   - Để có Token, bạn mở Swagger của Auth Service (http://localhost:5001/swagger/index.html), gọi hàm Register hoặc Login để lấy `accessToken`.
   - Quay lại Swagger của Club Service, nhấn nút **Authorize** ở góc phải trên cùng, nhập chuỗi `Bearer <chuỗi_token_của_bạn>`.
   - Giờ bạn đã có thể test các hàm POST thành công.

### Cách 2: Postman Collection
- Import URL Swagger của Club Service `http://localhost:5002/swagger/v1/swagger.json` trực tiếp vào Postman.
- Postman sẽ tự động sinh ra collection với các hàm tương ứng để test.

### Cách 3: Test gRPC
- Do gRPC giao tiếp qua chuẩn nhị phân (HTTP/2), bạn không thể test qua Swagger.
- Tải phần mềm **[gRPCurl](https://github.com/fullstorydev/grpcurl)** hoặc **[Postman](https://blog.postman.com/postman-now-supports-grpc/)** (bản mới hỗ trợ gRPC).
- Cấu hình gọi tới Server `localhost:9001`.
- Import file `src/Shared/Shared.Kernel/Grpc/Protos/club.proto` vào Postman.
- Bạn sẽ thấy danh sách các hàm: `CheckClubExists`, `GetClubInfo`, `IsClubManager`. Hãy truyền `club_id` vào JSON body và nhấn Send.
